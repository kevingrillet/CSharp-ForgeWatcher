<#
.SYNOPSIS
    Remet le dépôt Forge Watcher à l'état « sorti de clone » : supprime les sorties de
    compilation (bin, obj), le dossier « publish » et les rapports de tests.

.DESCRIPTION
    Deux temps :

      1. « dotnet clean » sur la solution, en Debug et en Release, pour que MSBuild oublie ce
         qu'il croit savoir (caches de build incrémental) ;
      2. suppression physique de tous les dossiers « bin » et « obj » du dépôt, du dossier
         « publish » et des dossiers « TestResults ».

    L'étape 2 est nécessaire : « dotnet clean » ne supprime que ce qu'il a produit lors de la
    dernière compilation, et laisse derrière lui les binaires d'un autre framework cible, d'un
    autre RID ou d'un projet retiré de la solution.

    Tous ces dossiers sont ignorés par Git : rien de versionné n'est touché. Le contenu de
    .git, l'index CodeGraph (.codegraph) et vos données locales (config.json, state.json) sont
    explicitement épargnés.

    Le script accepte -WhatIf pour lister ce qui serait supprimé sans rien effacer.

.PARAMETER Tout
    Supprime en plus les caches d'environnement de développement : .vs (Visual Studio) et les
    rapports .trx laissés à la racine. À utiliser quand l'IDE se comporte bizarrement.

.PARAMETER SansDotnetClean
    Saute l'étape « dotnet clean » et se contente de la suppression des dossiers. Utile quand
    la solution ne compile plus du tout, ou sans SDK .NET installé.

.EXAMPLE
    .\scripts\nettoyer.ps1 -WhatIf
    Affiche la liste de ce qui serait supprimé, sans rien effacer.

.EXAMPLE
    .\scripts\nettoyer.ps1
    Nettoyage standard.

.EXAMPLE
    .\scripts\nettoyer.ps1 -Tout
    Nettoyage standard + caches d'IDE.
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [switch] $Tout,

    [switch] $SansDotnetClean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Le script vit dans « scripts/ » : la racine du dépôt est un cran au-dessus.
$racine = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $racine 'CSharp-ForgeWatcher.slnx'

if (-not (Test-Path -LiteralPath $solution)) {
    throw "Solution introuvable : $solution"
}

<#
.SYNOPSIS
    Renvoie le poids total, en octets, du contenu d'un dossier.
.DESCRIPTION
    Passe par une variable intermédiaire : sur un dossier vide, Measure-Object n'émet aucun
    objet, et « (…).Sum » lèverait une erreur sous Set-StrictMode.
#>
function Get-PoidsOctets {
    param([Parameter(Mandatory = $true)][string] $Dossier)

    $mesure = Get-ChildItem -LiteralPath $Dossier -Recurse -File -Force -ErrorAction SilentlyContinue |
        Measure-Object -Property Length -Sum
    if ($null -eq $mesure -or $null -eq $mesure.Sum) {
        return 0L
    }
    return [long] $mesure.Sum
}

# --- 1. dotnet clean -----------------------------------------------------------------------

if (-not $SansDotnetClean) {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Warning 'SDK .NET introuvable : l''étape « dotnet clean » est sautée.'
    }
    else {
        foreach ($configuration in @('Debug', 'Release')) {
            Write-Host ''
            Write-Host "==> dotnet clean ($configuration)" -ForegroundColor Cyan

            if ($PSCmdlet.ShouldProcess("$solution ($configuration)", 'dotnet clean')) {
                # Verbosité minimale : seules les erreurs comptent, le détail MSBuild n'apporte
                # rien ici — l'inventaire ci-dessous dit déjà ce qui disparaît.
                & dotnet clean $solution -c $configuration --nologo --verbosity quiet
                # Échec non bloquant : la suppression des dossiers qui suit fait le travail de
                # toute façon, et « dotnet clean » échoue légitimement si rien n'a été compilé.
                if ($LASTEXITCODE -ne 0) {
                    Write-Warning "dotnet clean ($configuration) a renvoyé le code $LASTEXITCODE ; on continue."
                }
            }
        }
    }
}

# --- 2. Inventaire des dossiers à supprimer ------------------------------------------------

# Dossiers dans lesquels on ne descend jamais : versionnés, gérés par un autre outil, ou
# contenant des données locales qu'on ne veut pas perdre.
$segmentsIgnores = @('.git', '.codegraph', 'node_modules')

$nomsASupprimer = @('bin', 'obj', 'publish', 'TestResults')
if ($Tout) {
    $nomsASupprimer += '.vs'
}

Write-Host ''
Write-Host '==> Inventaire' -ForegroundColor Cyan

$candidats = Get-ChildItem -LiteralPath $racine -Directory -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object {
        # Écrit en plusieurs instructions plutôt qu'en une condition sur deux lignes :
        # Windows PowerShell 5.1 n'accepte pas un opérateur en fin de ligne hors parenthèses.
        if ($nomsASupprimer -notcontains $_.Name) {
            return $false
        }
        $chemin = $_.FullName.Substring($racine.Length)
        $segments = $chemin.Split([System.IO.Path]::DirectorySeparatorChar)
        $traverseUnDossierIgnore = $segments | Where-Object { $segmentsIgnores -contains $_ }
        return -not $traverseUnDossierIgnore
    } |
    Sort-Object FullName

# On ne garde que les dossiers de plus haut niveau : inutile de supprimer obj\Debug\net9.0
# quand on s'apprête à supprimer obj. Le tri ci-dessus garantit que le parent passe en premier.
$aSupprimer = New-Object System.Collections.Generic.List[System.IO.DirectoryInfo]
foreach ($candidat in $candidats) {
    $dejaCouvert = $false
    foreach ($retenu in $aSupprimer) {
        $prefixe = $retenu.FullName + [System.IO.Path]::DirectorySeparatorChar
        if ($candidat.FullName.StartsWith($prefixe, [System.StringComparison]::OrdinalIgnoreCase)) {
            $dejaCouvert = $true
            break
        }
    }
    if (-not $dejaCouvert) {
        $aSupprimer.Add($candidat)
    }
}

$fichiersASupprimer = @()
if ($Tout) {
    $fichiersASupprimer = @(Get-ChildItem -LiteralPath $racine -File -Filter '*.trx' -ErrorAction SilentlyContinue)
}

if ($aSupprimer.Count -eq 0 -and $fichiersASupprimer.Count -eq 0) {
    Write-Host 'Rien à supprimer : le dépôt est déjà propre.' -ForegroundColor Green
    return
}

# --- 3. Suppression ------------------------------------------------------------------------

$octetsLiberes = 0L
$supprimes = 0

foreach ($dossier in $aSupprimer) {
    $poids = Get-PoidsOctets -Dossier $dossier.FullName

    $chemin = $dossier.FullName.Substring($racine.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar)
    Write-Host ("  {0,-60} {1,8:N1} Mo" -f $chemin, ($poids / 1MB))

    if ($PSCmdlet.ShouldProcess($dossier.FullName, 'Supprimer le dossier')) {
        try {
            Remove-Item -LiteralPath $dossier.FullName -Recurse -Force
            $octetsLiberes += $poids
            $supprimes++
        }
        catch {
            # Cas courant : un fichier verrouillé par Visual Studio, l'application en cours
            # d'exécution ou l'antivirus. On signale et on continue le reste du nettoyage.
            Write-Warning "Suppression impossible : $chemin — $($_.Exception.Message)"
        }
    }
}

foreach ($fichier in $fichiersASupprimer) {
    Write-Host ("  {0,-60} {1,8:N1} Mo" -f $fichier.Name, ($fichier.Length / 1MB))
    if ($PSCmdlet.ShouldProcess($fichier.FullName, 'Supprimer le fichier')) {
        Remove-Item -LiteralPath $fichier.FullName -Force
        $octetsLiberes += $fichier.Length
        $supprimes++
    }
}

Write-Host ''
Write-Host ("Nettoyage terminé : {0} élément(s) supprimé(s), {1:N1} Mo libéré(s)." -f `
        $supprimes, ($octetsLiberes / 1MB)) -ForegroundColor Green
