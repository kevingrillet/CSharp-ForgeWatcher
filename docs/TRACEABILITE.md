# Traçabilité : spec → scénario → test

Chaque comportement spécifié est illustré par un **scénario Gherkin** ([`features/`](features/))
et vérifié par au moins un **test** portant la catégorie de même identifiant.

```powershell
dotnet test --filter TestCategory=SPEC-EVT-005
```

Des tests de garde (`tests/CSharpForgeWatcher.Tests/Features/FeatureCoverageTests.cs`)
maintiennent ce tableau honnête : ils échouent si un scénario cite une spec sans test, ou si
une spec testée n'est illustrée par aucun scénario.

## Pull requests

| Spec | Comportement | Tests |
|---|---|---|
| SPEC-EVT-001 | Nouvelle PR créée | `Detection/NewPullRequestRuleTests.cs`, `Detection/PullRequestEventDetectorTests.cs`, `Monitoring/PullRequestMonitorTests.cs` |
| SPEC-EVT-002 | Ajout comme relecteur | `Detection/ReviewerAssignedRuleTests.cs`, `Detection/PullRequestEventDetectorTests.cs` |
| SPEC-EVT-003 | Vote sur ma PR | `Detection/VoteChangedRuleTests.cs` |
| SPEC-EVT-004 | Commentaire sur ma PR | `Detection/NewCommentRuleTests.cs`, `Monitoring/PullRequestMonitorTests.cs` |
| SPEC-EVT-005 | Réponse à mon commentaire | `Detection/NewCommentRuleTests.cs` |
| SPEC-EVT-006 | Mention (@moi) | `Detection/NewCommentRuleTests.cs` |
| SPEC-EVT-007 | Commentaire sur une PR que je relis | `Detection/NewCommentRuleTests.cs` |
| SPEC-EVT-008 | Discussion résolue / réactivée | `Detection/ThreadStatusChangedRuleTests.cs` |
| SPEC-EVT-009 | Changement d'état de PR | `Detection/PullRequestStateChangedRuleTests.cs`, `Monitoring/PullRequestMonitorTests.cs` |

## Pipelines

| Spec | Comportement | Tests |
|---|---|---|
| SPEC-PIPE-001 | Pipeline en échec | `Detection/PipelineRulesTests.cs`, `Monitoring/PipelineMonitoringTests.cs` |
| SPEC-PIPE-002 | Retour au vert | `Detection/PipelineRulesTests.cs`, `Monitoring/PipelineMonitoringTests.cs` |
| SPEC-PIPE-003 | Sélection des pipelines | `Monitoring/PipelineMonitoringTests.cs` (purge) ; UI : `Views/SettingsForm.cs` |
| SPEC-PIPE-004 | Une seule requête par projet | `Monitoring/PipelineMonitoringTests.cs` |
| SPEC-PIPE-005 | Isolation des erreurs | `Monitoring/PipelineMonitoringTests.cs` |
| SPEC-PIPE-006 | Pipelines seuls = configuration valide | `Monitoring/PipelineMonitoringTests.cs` |

## Cycle de surveillance

| Spec | Comportement | Tests |
|---|---|---|
| SPEC-POLL-001 | Amorçage silencieux | `Monitoring/PullRequestMonitorTests.cs`, `Monitoring/PipelineMonitoringTests.cs`, `Detection/NewCommentRuleTests.cs` |
| SPEC-POLL-002 | Isolation des erreurs par dépôt | `Monitoring/PullRequestMonitorTests.cs` |
| SPEC-POLL-003 | Portée de lecture des discussions | `Monitoring/PullRequestMonitorTests.cs`, `Detection/NewCommentRuleTests.cs` |
| SPEC-POLL-004 | Échec d'authentification | `Monitoring/PullRequestMonitorTests.cs` |
| SPEC-POLL-005 | Réessai des erreurs transitoires | `Monitoring/ResilientSourceControlGatewayTests.cs` |

## Configuration et notifications

| Spec | Comportement | Tests |
|---|---|---|
| SPEC-CFG-001 | PAT jamais en clair | `Configuration/ConfigurationServiceTests.cs` |
| SPEC-CFG-002 | Sélection projets / dépôts | `Monitoring/PullRequestMonitorTests.cs` (purge) ; UI : `Views/SettingsForm.cs` |
| SPEC-CFG-003 | Validation de configuration | `Configuration/WatcherConfigurationTests.cs`, `Monitoring/PullRequestMonitorTests.cs` |
| SPEC-CFG-004 | Application à chaud | `Configuration/ConfigurationServiceTests.cs`, `Links/AzureDevOpsLinkBuilderTests.cs` |
| SPEC-CFG-007 | Réglages fournisseur et thème | `Theming/ThemeResolverTests.cs`, `Configuration/WatcherConfigurationTests.cs` |
| SPEC-CFG-008 | Comptes multiples, état cloisonné | `Monitoring/MultiAccountMonitoringTests.cs`, `Configuration/ConfigurationServiceTests.cs`, `Configuration/WatcherConfigurationTests.cs` |
| SPEC-NOTIF-001 | Clic → lien profond | `Monitoring/PullRequestMonitorTests.cs`, `Links/AzureDevOpsLinkBuilderTests.cs` |
| SPEC-NOTIF-002 | Pas de rafale (synthèse) | `Notifications/NotificationDispatcherTests.cs` |
| SPEC-NOTIF-003 | Filtres par type | `Notifications/NotificationDispatcherTests.cs`, `Configuration/WatcherConfigurationTests.cs`, `Monitoring/PipelineMonitoringTests.cs` |
| SPEC-NOTIF-004 | Robustesse de l'affichage | `Notifications/NotificationDispatcherTests.cs` |
| SPEC-LINK-001..003 | Construction des URL | `Links/AzureDevOpsLinkBuilderTests.cs`, `Links/GitHubLinkBuilderTests.cs` |
| SPEC-LINK-004 | Adresse fournie par la forge | `Detection/NewCommentRuleTests.cs` |

## Forges

| Spec | Comportement | Tests |
|---|---|---|
| SPEC-FORGE-002 | Fournisseur configurable, valeur non implémentée refusée | `Configuration/WatcherConfigurationTests.cs`, `Links/ProviderAwareLinkBuilderTests.cs`, `Links/GitHubLinkBuilderTests.cs`, `Links/GitLabLinkBuilderTests.cs` |
| SPEC-FORGE-003 | Formats d'URL par forge | `Links/GitHubLinkBuilderTests.cs`, `Links/GitLabLinkBuilderTests.cs`, `Links/ProviderAwareLinkBuilderTests.cs` |
| SPEC-FORGE-006 | Identifiants 64 bits | `Links/GitHubLinkBuilderTests.cs`, `Detection/NewCommentRuleTests.cs`, `Detection/PipelineRulesTests.cs` |
| SPEC-FORGE-007 | Capacité absente → règle muette | `Detection/ThreadStatusChangedRuleTests.cs` |

## Apparence

| Spec | Comportement | Tests |
|---|---|---|
| SPEC-UI-THEME-001 | Trois positions | `Theming/ThemeResolverTests.cs` |
| SPEC-UI-THEME-002 | Résolution du thème effectif | `Theming/ThemeResolverTests.cs` |

## Zones sans test automatisé

Assumé, avec la raison :

| Zone | Specs | Pourquoi | Comment c'est vérifié |
|---|---|---|---|
| Rendu WinForms | SPEC-UI-THEME-003, -004, SPEC-UI-ICON-001, SPEC-NOTIF-005 | Coût d'un harnais d'UI disproportionné pour un outil interne ; l'UI ne porte aucune règle métier | Vérification manuelle ; la logique extractible (résolution du thème) est testée |
| Bascule de langue à l'écran | SPEC-UI-LANG-001 (repeinte des fenêtres) | WinForms compose ses libellés à la construction : le vérifier demanderait d'ouvrir de vraies fenêtres | Vérification manuelle ; la **résolution** de la langue et la **parité des catalogues** sont testées (`LanguageResolverTests`, `TextCatalogueTests`) |
| Toasts Windows | SPEC-NOTIF-004 (canal réel) | Dépend de l'état du système (stratégies de groupe, mode concentration) | Bouton *Tester une notification* (onglet Avancé) + repli automatique testé |
| DPAPI | SPEC-CFG-001 (chiffrement réel) | Dépend du compte Windows | Testé via un double réversible ; l'échec de déchiffrement est couvert |
| Registre | SPEC-CFG-006 | Écrirait dans le `HKCU` de la machine de test | Vérification manuelle ; l'état affiché est relu du registre |
| Système de fichiers | SPEC-CFG-005 | Chemins réels sous `%APPDATA%` | Écriture atomique et tolérance aux fichiers corrompus par construction (`JsonFileStore`) |
| Reprise de l'ancien nom | SPEC-CFG-005 (reprise), ADR-0006 | Déplacerait un dossier réel de `%APPDATA%` et écrirait dans le `HKCU` de la machine de test | Vérification manuelle ; chaque étape est indépendante, idempotente, et son échec est journalisé sans empêcher le démarrage |
| Appels HTTP réels | — | Exigerait un serveur de test et un jeton en CI, pour chacune des trois forges | Mappage isolé dans `AzureDevOpsMapper`, `GitHubMapper` et `GitLabMapper` ; plomberie commune dans `RestGatewayBase` ; erreurs classées, testées via le décorateur de résilience |
| Contrat d'abstraction | SPEC-FORGE-001, -004, -005 | Contraintes d'architecture, pas comportements exécutables | Subagent `relecteur-architecture` + balayages du skill `respecter-architecture` |

Un mot sur la dernière ligne, qui vaut aussi pour les adaptateurs de forge : la couche de
test ne référence **que** `Domain` et `Application` (cf. le skill `respecter-architecture`).
Les mappeurs REST, bien que faits de fonctions pures, vivent dans `Infrastructure` avec leurs
DTO internes ; ils ne sont donc pas testés unitairement. Ce qui est vérifiable sans réseau a
été remonté là où il l'est : les formats d'URL, la sélection par fournisseur, la validation de
configuration et la détection de mention sont dans `Application` ou `Domain`, et couverts.
