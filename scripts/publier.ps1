<#
.SYNOPSIS
    Restaure, compile puis publie Forge Watcher dans le dossier « publish » à la racine du dépôt.

.DESCRIPTION
    Enchaîne les trois étapes de production d'un exécutable distribuable :

      1. « dotnet restore » sur la solution ;
      2. « dotnet build » sur la solution (tous les projets, avertissements = erreurs) ;
      3. « dotnet publish » du seul projet d'interface, vers un dossier de sortie propre.

    L'étape 2 compile toute la solution — et pas seulement le projet publié — pour que la
    compilation échoue ici, en local, plutôt qu'en intégration continue.

    Le dossier de sortie est vidé avant publication : sans cela, les fichiers d'une
    publication précédente (ancienne DLL, ancien fichier de configuration) survivraient dans
    l'archive livrée.

    Le dossier « publish » est ignoré par Git (voir .gitignore).

.PARAMETER Configuration
    Configuration de compilation. « Release » par défaut, comme la CI de release.

.PARAMETER Runtime
    Identifiant de runtime cible. « win-x64 » par défaut : la solution ne tourne que sur
    Windows (WinForms, DPAPI, registre).

.PARAMETER Autonome
    Produit une version autonome en fichier unique (~150 Mo), qui ne nécessite pas le runtime
    .NET Desktop sur le poste cible. Par défaut, la version produite est légère et exige le
    runtime .NET 9 Desktop.

.PARAMETER Version
    Numéro de version à graver dans le binaire (ex. « 1.2.3 »). Par défaut, celui de
    Directory.Build.props.

.PARAMETER Sortie
    Dossier de sortie. « publish » à la racine du dépôt par défaut.

.EXAMPLE
    .\scripts\publier.ps1
    Version légère dans publish\, à partir de la version déclarée dans Directory.Build.props.

.EXAMPLE
    .\scripts\publier.ps1 -Autonome -Version 1.2.3
    Exécutable unique et autonome, estampillé 1.2.3.
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string] $Configuration = 'Release',

    [ValidateNotNullOrEmpty()]
    [string] $Runtime = 'win-x64',

    [switch] $Autonome,

    [ValidatePattern('^$|^\d+\.\d+\.\d+(\.\d+)?$')]
    [string] $Version = '',

    [string] $Sortie = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Le script vit dans « scripts/ » : la racine du dépôt est un cran au-dessus.
$racine = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $racine 'CSharp-ForgeWatcher.slnx'
$projetUi = Join-Path $racine 'src\CSharpForgeWatcher.Ui'
if ([string]::IsNullOrWhiteSpace($Sortie)) {
    $Sortie = Join-Path $racine 'publish'
}

<#
.SYNOPSIS
    Lance « dotnet » et interrompt le script si la commande échoue.
.DESCRIPTION
    dotnet ne lève pas d'exception PowerShell : sans ce contrôle du code de retour, un build
    en échec passerait inaperçu et l'on publierait les binaires de la fois précédente.
#>
function Invoke-Dotnet {
    param(
        [Parameter(Mandatory = $true)][string] $Etape,
        [Parameter(Mandatory = $true)][string[]] $Arguments
    )

    Write-Host ''
    Write-Host "==> $Etape" -ForegroundColor Cyan
    Write-Host "    dotnet $($Arguments -join ' ')" -ForegroundColor DarkGray

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Etape : échec (dotnet a renvoyé le code $LASTEXITCODE)."
    }
}

# --- Vérifications préalables ------------------------------------------------------------

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Le SDK .NET est introuvable : « dotnet » n''est pas dans le PATH.'
}

if (-not (Test-Path -LiteralPath $solution)) {
    throw "Solution introuvable : $solution"
}

# Combine (et non Join-Path) : si -Sortie est un chemin absolu, il est conservé tel quel.
$sortieComplete = [System.IO.Path]::GetFullPath(
    [System.IO.Path]::Combine((Get-Location).Path, $Sortie))
$racineComplete = [System.IO.Path]::GetFullPath($racine)

# Garde-fou : on refuse de vider un dossier qui contient le dépôt lui-même. Une faute de
# frappe sur -Sortie ne doit pas effacer le code source.
$prefixeSortie = $sortieComplete.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
if ($racineComplete -eq $sortieComplete -or
    $racineComplete.StartsWith($prefixeSortie, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Dossier de sortie refusé : « $sortieComplete » contient le dépôt."
}

# --- Étapes ------------------------------------------------------------------------------

$proprietes = @()
if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $proprietes += "-p:Version=$Version"
}

Invoke-Dotnet -Etape 'Restauration des dépendances' -Arguments (@(
        'restore', $solution, '--nologo'
    ))

Invoke-Dotnet -Etape "Compilation de la solution ($Configuration)" -Arguments (@(
        'build', $solution, '--no-restore', '-c', $Configuration, '--nologo'
    ) + $proprietes)

if (Test-Path -LiteralPath $sortieComplete) {
    Write-Host ''
    Write-Host "==> Nettoyage du dossier de sortie" -ForegroundColor Cyan
    Write-Host "    $sortieComplete" -ForegroundColor DarkGray
    Remove-Item -LiteralPath $sortieComplete -Recurse -Force
}

# Version autonome : tout est embarqué dans ForgeWatcher.exe, rien à installer sur le poste.
# Version légère (défaut) : le runtime .NET 9 Desktop doit être présent sur le poste.
$optionsAutonomie = if ($Autonome) {
    @('--self-contained', 'true', '-p:PublishSingleFile=true')
}
else {
    @('--self-contained', 'false')
}

# Pas de --no-restore ici : la publication ciblée sur un runtime a besoin de sa propre
# restauration (les paquets natifs du RID ne sont pas résolus par le restore de l'étape 1).
Invoke-Dotnet -Etape "Publication ($Runtime)" -Arguments (@(
        'publish', $projetUi, '-c', $Configuration, '-r', $Runtime, '-o', $sortieComplete, '--nologo'
    ) + $optionsAutonomie + $proprietes)

# --- Compte rendu --------------------------------------------------------------------------

$executable = Join-Path $sortieComplete 'ForgeWatcher.exe'
if (-not (Test-Path -LiteralPath $executable)) {
    throw "Publication terminée mais ForgeWatcher.exe est introuvable dans « $sortieComplete »."
}

$fichiers = @(Get-ChildItem -LiteralPath $sortieComplete -Recurse -File)
$poidsTotal = ($fichiers | Measure-Object -Property Length -Sum).Sum

Write-Host ''
Write-Host 'Publication réussie.' -ForegroundColor Green
Write-Host "  Exécutable : $executable"
Write-Host ("  Version    : {0}" -f (Get-Item -LiteralPath $executable).VersionInfo.ProductVersion)
Write-Host ("  Contenu    : {0:N0} fichier(s), {1:N1} Mo" -f $fichiers.Count, ($poidsTotal / 1MB))
Write-Host ''
Write-Host 'Copiez le dossier où vous voulez, lancez ForgeWatcher.exe, puis cochez'
Write-Host '« Démarrer avec Windows » dans les paramètres si besoin.' -ForegroundColor DarkGray
