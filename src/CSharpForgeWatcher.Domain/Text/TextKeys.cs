using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.Pipelines;
using CSharpForgeWatcher.Domain.PullRequests;

namespace CSharpForgeWatcher.Domain.Text;

/// <summary>
/// Clés du catalogue de textes (SPEC-UI-LANG-002).
/// </summary>
/// <remarks>
/// <para>
/// Des constantes plutôt que des chaînes libres : une clé mal orthographiée devient une
/// erreur de compilation au lieu d'un message manquant découvert par l'utilisateur. Les clés
/// dérivées d'une énumération passent par une méthode, ce qui garantit qu'une valeur ajoutée
/// à l'énumération est traitée partout de la même façon.
/// </para>
/// <para>
/// Ce fichier vit dans le domaine parce que les quatre couches y font référence, et qu'il ne
/// contient que des identifiants — aucune formulation, aucune langue.
/// </para>
/// </remarks>
public static class TextKeys
{
    /// <summary>Fragment vide (partie facultative absente).</summary>
    public const string Empty = "Common.Empty";

    /// <summary>Nom du produit, identique dans toutes les langues.</summary>
    public const string AppName = "Common.AppName";

    /// <summary>Clé du libellé court d'un type d'activité.</summary>
    public static string KindLabel(NotificationKind kind) => $"Kind.{kind}.Label";

    /// <summary>Clé de l'explication d'un type d'activité (info-bulle des préférences).</summary>
    public static string KindDescription(NotificationKind kind) => $"Kind.{kind}.Description";

    /// <summary>Clé de l'action d'un vote (« a approuvé »), employée dans une phrase.</summary>
    public static string VoteAction(ReviewerVote vote) => $"Vote.{vote}.Action";

    /// <summary>Clé du libellé autonome d'un vote (« Approuvé »), employé dans une liste.</summary>
    public static string VoteLabel(ReviewerVote vote) => $"Vote.{vote}.Label";

    /// <summary>Clé du libellé d'un état de pull request.</summary>
    public static string PullRequestStatusLabel(PullRequestStatus status) => $"PrStatus.{status}";

    /// <summary>Clé du libellé d'un résultat d'exécution de pipeline.</summary>
    public static string PipelineResultLabel(PipelineRunResult result) => $"PipelineResult.{result}";

    /// <summary>Clé du libellé d'un état de discussion.</summary>
    public static string ThreadStatusLabel(CommentThreadStatus status) => $"ThreadStatus.{status}";

    /// <summary>Corps des notifications produits par les règles de détection.</summary>
    public static class Event
    {
        /// <summary>Commentaire : auteur, fichier facultatif, extrait, suite facultative.</summary>
        public const string Comment = "Event.Comment";

        /// <summary>Fragment « fichier commenté ».</summary>
        public const string CommentFile = "Event.Comment.File";

        /// <summary>Fragment « et N autres messages ».</summary>
        public const string CommentMore = "Event.Comment.More";

        /// <summary>Vote d'un relecteur : qui, et quoi.</summary>
        public const string Vote = "Event.Vote";

        /// <summary>Demande de relecture.</summary>
        public const string ReviewerAssigned = "Event.ReviewerAssigned";

        /// <summary>Fragment « relecteur obligatoire ».</summary>
        public const string ReviewerRequired = "Event.ReviewerAssigned.Required";

        /// <summary>Nouvelle pull request.</summary>
        public const string PullRequestCreated = "Event.PullRequestCreated";

        /// <summary>Fragment « vers telle branche ».</summary>
        public const string PullRequestTarget = "Event.PullRequestCreated.Target";

        /// <summary>Fragment « brouillon ».</summary>
        public const string PullRequestDraft = "Event.PullRequestCreated.Draft";

        /// <summary>Pull request complétée et fusionnée.</summary>
        public const string PullRequestCompleted = "Event.PrStatus.Completed";

        /// <summary>Pull request abandonnée.</summary>
        public const string PullRequestAbandoned = "Event.PrStatus.Abandoned";

        /// <summary>Pull request réactivée.</summary>
        public const string PullRequestReactivated = "Event.PrStatus.Reactivated";

        /// <summary>Autre changement d'état, le libellé de l'état étant passé en argument.</summary>
        public const string PullRequestStatusOther = "Event.PrStatus.Other";

        /// <summary>Brouillon publié.</summary>
        public const string PullRequestDraftPublished = "Event.PrStatus.DraftPublished";

        /// <summary>Discussion marquée résolue.</summary>
        public const string ThreadResolved = "Event.Thread.Resolved";

        /// <summary>Discussion réactivée.</summary>
        public const string ThreadReactivated = "Event.Thread.Reactivated";

        /// <summary>Message de discussion suivi d'un extrait.</summary>
        public const string ThreadWithExcerpt = "Event.Thread.WithExcerpt";

        /// <summary>Exécution de pipeline en échec.</summary>
        public const string PipelineFailed = "Event.Pipeline.Failed";

        /// <summary>Exécution de pipeline de nouveau au vert.</summary>
        public const string PipelineRecovered = "Event.Pipeline.Recovered";

        /// <summary>Fragment « sur telle branche ».</summary>
        public const string PipelineBranch = "Event.Pipeline.Branch";

        /// <summary>Fragment « déclenché par ».</summary>
        public const string PipelineActor = "Event.Pipeline.Actor";

        /// <summary>Sujet d'un événement de pipeline : définition et exécution.</summary>
        public const string PipelineSubject = "Event.Pipeline.Subject";

        /// <summary>Sujet d'un événement de pull request : numéro et titre.</summary>
        public const string PullRequestSubject = "Event.PullRequest.Subject";
    }

    /// <summary>Comptes rendus de cycle et avertissements.</summary>
    public static class Poll
    {
        /// <summary>Configuration incomplète.</summary>
        public const string NotConfigured = "Poll.NotConfigured";

        /// <summary>Échec du cycle.</summary>
        public const string Failure = "Poll.Failure";

        /// <summary>Cycle déjà en cours.</summary>
        public const string Skipped = "Poll.Skipped";

        /// <summary>Décompte des pull requests suivies.</summary>
        public const string FollowedPullRequests = "Poll.Followed.PullRequests";

        /// <summary>Décompte des pipelines suivis.</summary>
        public const string FollowedPipelines = "Poll.Followed.Pipelines";

        /// <summary>Fragment « dont N en échec ».</summary>
        public const string FollowedPipelinesFailing = "Poll.Followed.PipelinesFailing";

        /// <summary>Cycle partiellement en échec : résumé et nombre d'avertissements.</summary>
        public const string PartialFailure = "Poll.PartialFailure";

        /// <summary>Surveillance pas encore démarrée.</summary>
        public const string Pending = "Poll.Pending";

        /// <summary>Titre de l'alerte « lecture impossible ».</summary>
        public const string ReadFailedTitle = "Poll.ReadFailed.Title";

        /// <summary>Avertissement préfixé du libellé de compte.</summary>
        public const string AccountPrefixed = "Poll.Warning.AccountPrefixed";

        /// <summary>Dépôt illisible.</summary>
        public const string RepositoryUnreadable = "Poll.Warning.Repository";

        /// <summary>Aucun dépôt lisible sur un compte.</summary>
        public const string NoRepositoryReadable = "Poll.Warning.NoRepository";

        /// <summary>Pull request disparue et non relisible.</summary>
        public const string PullRequestUnreadable = "Poll.Warning.PullRequest";

        /// <summary>Discussions illisibles.</summary>
        public const string ThreadsUnreadable = "Poll.Warning.Threads";

        /// <summary>Pipelines d'un espace illisibles.</summary>
        public const string PipelinesUnreadable = "Poll.Warning.Pipelines";

        /// <summary>Échec portant sur un compte identifié.</summary>
        public const string AccountFailed = "Poll.Warning.AccountFailed";

        /// <summary>Décompte des discussions ouvertes, dans le détail d'une PR.</summary>
        public const string ViewUnresolvedThreads = "Poll.View.UnresolvedThreads";

        /// <summary>Vote de l'utilisateur, dans le détail d'une PR.</summary>
        public const string ViewYourVote = "Poll.View.YourVote";

        /// <summary>Mention « brouillon », dans le détail d'une PR.</summary>
        public const string ViewDraft = "Poll.View.Draft";
    }

    /// <summary>Validation de la configuration et libellés de réglages.</summary>
    public static class Config
    {
        /// <summary>Aucun compte configuré.</summary>
        public const string NoAccount = "Config.NoAccount";

        /// <summary>Tous les comptes désactivés.</summary>
        public const string AllAccountsDisabled = "Config.AllAccountsDisabled";

        /// <summary>Aucun élément surveillé.</summary>
        public const string NoSelection = "Config.NoSelection";

        /// <summary>Intervalle trop court.</summary>
        public const string PollIntervalTooShort = "Config.PollIntervalTooShort";

        /// <summary>Seuil de notification invalide.</summary>
        public const string MaxNotificationsTooLow = "Config.MaxNotificationsTooLow";

        /// <summary>Adresse de forge manquante.</summary>
        public const string UrlMissing = "Config.UrlMissing";

        /// <summary>Adresse de forge invalide.</summary>
        public const string UrlInvalid = "Config.UrlInvalid";

        /// <summary>Jeton manquant.</summary>
        public const string TokenMissing = "Config.TokenMissing";

        /// <summary>Forge non prise en charge.</summary>
        public const string ProviderUnsupported = "Config.ProviderUnsupported";
    }

    /// <summary>
    /// Messages d'erreur des passerelles.
    /// </summary>
    /// <remarks>
    /// Les libellés qui <b>dépendent de la forge</b> — champ d'adresse, niveau intermédiaire de
    /// l'arborescence, portées de jeton — ne sont pas ici : leur clé se déduit du fournisseur,
    /// que le domaine ne connaît pas. Elles sont produites par
    /// <c>SourceControlProviderExtensions</c>, dans la couche application.
    /// </remarks>
    public static class Forge
    {
        /// <summary>Jeton refusé (401).</summary>
        public const string Unauthorized = "Forge.Error.Unauthorized";

        /// <summary>Accès refusé (403).</summary>
        public const string Forbidden = "Forge.Error.Forbidden";

        /// <summary>Quota d'appels épuisé (429).</summary>
        public const string RateLimited = "Forge.Error.RateLimited";

        /// <summary>Problème serveur (5xx).</summary>
        public const string ServerError = "Forge.Error.Server";

        /// <summary>Autre échec d'appel.</summary>
        public const string CallFailed = "Forge.Error.CallFailed";

        /// <summary>Réponse vide.</summary>
        public const string EmptyResponse = "Forge.Error.EmptyResponse";

        /// <summary>Réponse illisible.</summary>
        public const string UnreadableResponse = "Forge.Error.UnreadableResponse";

        /// <summary>Délai dépassé.</summary>
        public const string Timeout = "Forge.Error.Timeout";

        /// <summary>Serveur injoignable.</summary>
        public const string Unreachable = "Forge.Error.Unreachable";

        /// <summary>Message enrichi d'un conseil sur le jeton.</summary>
        public const string AuthAdvice = "Forge.Error.AuthAdvice";

        /// <summary>Message enrichi de la mention d'un réessai.</summary>
        public const string TransientAdvice = "Forge.Error.TransientAdvice";

        /// <summary>La forge n'a pas renvoyé d'identité pour le jeton.</summary>
        public const string NoIdentity = "Forge.Error.NoIdentity";

        /// <summary>Page HTML reçue à la place du JSON attendu.</summary>
        public const string HtmlResponse = "Forge.Error.HtmlResponse";

        /// <summary>Accès refusé, avec la portée propre à Azure DevOps.</summary>
        public const string ForbiddenAzureDevOps = "Forge.Error.ForbiddenAzureDevOps";

        /// <summary>Quota d'appels épuisé, avec le délai de réinitialisation s'il est connu.</summary>
        public const string QuotaExhausted = "Forge.Error.QuotaExhausted";

        /// <summary>Fragment « quota réinitialisé dans N minutes ».</summary>
        public const string QuotaReset = "Forge.Error.QuotaReset";

        /// <summary>Chemin de dépôt GitHub mal formé.</summary>
        public const string InvalidGitHubPath = "Forge.Error.InvalidGitHubPath";

        /// <summary>Dépôt GitHub incomplet dans la sélection.</summary>
        public const string IncompleteGitHubRepository = "Forge.Error.IncompleteGitHubRepository";

        /// <summary>Identifiant de projet GitLab inutilisable.</summary>
        public const string InvalidGitLabProject = "Forge.Error.InvalidGitLabProject";

        /// <summary>
        /// Espace synthétique regroupant les dépôts personnels d'un compte GitHub.
        /// </summary>
        /// <remarks>
        /// Seul « espace » que l'application invente : les autres portent le nom que la forge
        /// leur donne. L'adaptateur ne sait pas formuler, il renvoie donc cette clé comme nom,
        /// et l'arborescence la reconnaît (SPEC-UI-LANG-002).
        /// </remarks>
        public const string GitHubPersonalRepositories = "Forge.GitHub.PersonalRepositories";
    }

    /// <summary>Textes de l'interface.</summary>
    public static class Screen
    {
        /// <summary>Titre de la fenêtre de configuration.</summary>
        public const string SettingsTitle = "Screen.Settings.Title";

        /// <summary>Onglet « Comptes ».</summary>
        public const string TabAccounts = "Screen.Tab.Accounts";

        /// <summary>Onglet « Dépôts ».</summary>
        public const string TabRepositories = "Screen.Tab.Repositories";

        /// <summary>Onglet « Pipelines ».</summary>
        public const string TabPipelines = "Screen.Tab.Pipelines";

        /// <summary>Onglet « Préférences ».</summary>
        public const string TabPreferences = "Screen.Tab.Preferences";

        /// <summary>Onglet « Avancé ».</summary>
        public const string TabAdvanced = "Screen.Tab.Advanced";

        /// <summary>Bouton « Ajouter… ».</summary>
        public const string ButtonAdd = "Screen.Button.Add";

        /// <summary>Bouton « Modifier… ».</summary>
        public const string ButtonEdit = "Screen.Button.Edit";

        /// <summary>Bouton « Retirer ».</summary>
        public const string ButtonRemove = "Screen.Button.Remove";

        /// <summary>Bouton « Recharger ».</summary>
        public const string ButtonReload = "Screen.Button.Reload";

        /// <summary>Bouton « Enregistrer ».</summary>
        public const string ButtonSave = "Screen.Button.Save";

        /// <summary>Bouton « Annuler ».</summary>
        public const string ButtonCancel = "Screen.Button.Cancel";

        /// <summary>Bouton « Valider ».</summary>
        public const string ButtonConfirm = "Screen.Button.Confirm";

        /// <summary>Bouton « Fermer ».</summary>
        public const string ButtonClose = "Screen.Button.Close";

        /// <summary>Invite du champ de filtre.</summary>
        public const string FilterPlaceholder = "Screen.Filter.Placeholder";

        /// <summary>Explication de l'onglet « Comptes ».</summary>
        public const string AccountsExplanation = "Screen.Accounts.Explanation";

        /// <summary>Aucun compte configuré.</summary>
        public const string AccountsEmpty = "Screen.Accounts.Empty";

        /// <summary>Décompte des comptes et des éléments suivis.</summary>
        public const string AccountsSummary = "Screen.Accounts.Summary";

        /// <summary>Confirmation de retrait d'un compte.</summary>
        public const string AccountRemoveConfirm = "Screen.Accounts.RemoveConfirm";

        /// <summary>Détail « les N éléments seront oubliés ».</summary>
        public const string AccountRemoveDetail = "Screen.Accounts.RemoveDetail";

        /// <summary>Titre de l'arborescence des dépôts.</summary>
        public const string RepositoriesTreeTitle = "Screen.Repositories.TreeTitle";

        /// <summary>Titre de la liste des dépôts surveillés.</summary>
        public const string RepositoriesSelectionTitle = "Screen.Repositories.SelectionTitle";

        /// <summary>Invite initiale de l'onglet « Dépôts ».</summary>
        public const string RepositoriesHint = "Screen.Repositories.Hint";

        /// <summary>Décompte des dépôts surveillés.</summary>
        public const string RepositoriesCount = "Screen.Repositories.Count";

        /// <summary>Titre de l'arborescence des pipelines.</summary>
        public const string PipelinesTreeTitle = "Screen.Pipelines.TreeTitle";

        /// <summary>Titre de la liste des pipelines surveillés.</summary>
        public const string PipelinesSelectionTitle = "Screen.Pipelines.SelectionTitle";

        /// <summary>Invite initiale de l'onglet « Pipelines ».</summary>
        public const string PipelinesHint = "Screen.Pipelines.Hint";

        /// <summary>Décompte des pipelines surveillés.</summary>
        public const string PipelinesCount = "Screen.Pipelines.Count";

        /// <summary>Nœud « Chargement… ».</summary>
        public const string TreeLoading = "Screen.Tree.Loading";

        /// <summary>Nœud « aucun élément ».</summary>
        public const string TreeEmpty = "Screen.Tree.Empty";

        /// <summary>Nœud « aucun résultat » (filtre actif).</summary>
        public const string TreeNoMatch = "Screen.Tree.NoMatch";

        /// <summary>Nœud « chargement impossible ».</summary>
        public const string TreeLoadFailed = "Screen.Tree.LoadFailed";

        /// <summary>Compte désactivé, dans l'arborescence.</summary>
        public const string TreeAccountDisabled = "Screen.Tree.AccountDisabled";

        /// <summary>Invitation à ajouter un compte.</summary>
        public const string TreeAddAccountFirst = "Screen.Tree.AddAccountFirst";

        /// <summary>Décompte des comptes à déplier.</summary>
        public const string TreeAccountsLoaded = "Screen.Tree.AccountsLoaded";

        /// <summary>Décompte des espaces chargés pour un compte.</summary>
        public const string TreeScopesLoaded = "Screen.Tree.ScopesLoaded";

        /// <summary>Décompte des éléments chargés pour un espace.</summary>
        public const string TreeItemsLoaded = "Screen.Tree.ItemsLoaded";

        /// <summary>Échec de chargement, avec sa cause.</summary>
        public const string TreeLoadFailedWith = "Screen.Tree.LoadFailedWith";

        /// <summary>Délai dépassé pendant un chargement.</summary>
        public const string TreeLoadTimedOut = "Screen.Tree.LoadTimedOut";

        /// <summary>Groupe « Me notifier quand… ».</summary>
        public const string PreferencesKinds = "Screen.Preferences.Kinds";

        /// <summary>Groupe « Comportement et apparence ».</summary>
        public const string PreferencesBehaviour = "Screen.Preferences.Behaviour";

        /// <summary>Case « problèmes de fonctionnement ».</summary>
        public const string PreferencesOperationalErrors = "Screen.Preferences.OperationalErrors";

        /// <summary>Libellé du réglage de langue.</summary>
        public const string PreferencesLanguage = "Screen.Preferences.Language";

        /// <summary>Libellé du réglage de thème.</summary>
        public const string PreferencesTheme = "Screen.Preferences.Theme";

        /// <summary>Libellé de l'intervalle de surveillance.</summary>
        public const string PreferencesInterval = "Screen.Preferences.Interval";

        /// <summary>Libellé du seuil de synthèse.</summary>
        public const string PreferencesMaxNotifications = "Screen.Preferences.MaxNotifications";

        /// <summary>Libellé de la portée de lecture des discussions.</summary>
        public const string PreferencesThreadScope = "Screen.Preferences.ThreadScope";

        /// <summary>Libellé du délai de relecture des autres PR.</summary>
        public const string PreferencesRefreshMinutes = "Screen.Preferences.RefreshMinutes";

        /// <summary>Case « me notifier de mes propres actions ».</summary>
        public const string PreferencesOwnActions = "Screen.Preferences.OwnActions";

        /// <summary>Case « émettre un son ».</summary>
        public const string PreferencesSound = "Screen.Preferences.Sound";

        /// <summary>Case « démarrer avec Windows ».</summary>
        public const string PreferencesStartup = "Screen.Preferences.Startup";

        /// <summary>Portée « seulement les PR qui me concernent ».</summary>
        public const string ThreadScopeInvolved = "Screen.ThreadScope.Involved";

        /// <summary>Portée « toutes les PR surveillées ».</summary>
        public const string ThreadScopeAll = "Screen.ThreadScope.All";

        /// <summary>Section « fichiers de l'application ».</summary>
        public const string AdvancedFiles = "Screen.Advanced.Files";

        /// <summary>Ligne « configuration : chemin ».</summary>
        public const string AdvancedConfigurationFile = "Screen.Advanced.ConfigurationFile";

        /// <summary>Ligne « état surveillé : chemin ».</summary>
        public const string AdvancedStateFile = "Screen.Advanced.StateFile";

        /// <summary>Ligne « journal : chemin ».</summary>
        public const string AdvancedLogFile = "Screen.Advanced.LogFile";

        /// <summary>Bouton d'ouverture du dossier de données.</summary>
        public const string AdvancedOpenFolder = "Screen.Advanced.OpenFolder";

        /// <summary>Échec d'ouverture du dossier.</summary>
        public const string AdvancedOpenFolderFailed = "Screen.Advanced.OpenFolderFailed";

        /// <summary>Section « vérification des notifications ».</summary>
        public const string AdvancedNotificationCheck = "Screen.Advanced.NotificationCheck";

        /// <summary>Explication du test de notification.</summary>
        public const string AdvancedNotificationHint = "Screen.Advanced.NotificationHint";

        /// <summary>Bouton de test de notification.</summary>
        public const string AdvancedTestNotification = "Screen.Advanced.TestNotification";

        /// <summary>Section « réinitialisation ».</summary>
        public const string AdvancedReset = "Screen.Advanced.Reset";

        /// <summary>Explication de la réinitialisation.</summary>
        public const string AdvancedResetHint = "Screen.Advanced.ResetHint";

        /// <summary>Bouton de réinitialisation.</summary>
        public const string AdvancedResetButton = "Screen.Advanced.ResetButton";

        /// <summary>Confirmation de réinitialisation.</summary>
        public const string AdvancedResetConfirm = "Screen.Advanced.ResetConfirm";

        /// <summary>État réinitialisé.</summary>
        public const string AdvancedResetDone = "Screen.Advanced.ResetDone";

        /// <summary>Titre de la notification d'exemple.</summary>
        public const string SampleNotification = "Screen.Advanced.SampleNotification";

        /// <summary>Titre de l'exemple de pull request.</summary>
        public const string SamplePullRequestTitle = "Screen.Advanced.SamplePullRequest";

        /// <summary>Configuration incomplète, à l'enregistrement.</summary>
        public const string SaveInvalid = "Screen.Save.Invalid";

        /// <summary>Démarrage automatique non modifiable.</summary>
        public const string SaveStartupFailed = "Screen.Save.StartupFailed";

        /// <summary>Titre de la fenêtre de compte, en création.</summary>
        public const string AccountFormNew = "Screen.Account.New";

        /// <summary>Titre de la fenêtre de compte, en édition.</summary>
        public const string AccountFormEdit = "Screen.Account.Edit";

        /// <summary>Libellé du champ « forge ».</summary>
        public const string AccountProvider = "Screen.Account.Provider";

        /// <summary>Libellé du champ « libellé ».</summary>
        public const string AccountLabel = "Screen.Account.Label";

        /// <summary>Invite du champ « libellé ».</summary>
        public const string AccountLabelPlaceholder = "Screen.Account.LabelPlaceholder";

        /// <summary>Libellé du champ « jeton ».</summary>
        public const string AccountToken = "Screen.Account.Token";

        /// <summary>Invite du champ « jeton », jeton déjà enregistré.</summary>
        public const string AccountTokenStored = "Screen.Account.TokenStored";

        /// <summary>Invite du champ « jeton », aucun jeton enregistré.</summary>
        public const string AccountTokenEmpty = "Screen.Account.TokenEmpty";

        /// <summary>Lien de création de jeton.</summary>
        public const string AccountTokenPage = "Screen.Account.TokenPage";

        /// <summary>Case « surveiller ce compte ».</summary>
        public const string AccountEnabled = "Screen.Account.Enabled";

        /// <summary>Bouton de test de connexion.</summary>
        public const string AccountTest = "Screen.Account.Test";

        /// <summary>Connexion non testée.</summary>
        public const string AccountTestIdle = "Screen.Account.TestIdle";

        /// <summary>Test en cours.</summary>
        public const string AccountTestRunning = "Screen.Account.TestRunning";

        /// <summary>Test réussi.</summary>
        public const string AccountTestOk = "Screen.Account.TestOk";

        /// <summary>Adresse manquante au moment du test.</summary>
        public const string AccountTestUrlMissing = "Screen.Account.TestUrlMissing";

        /// <summary>Jeton manquant au moment du test.</summary>
        public const string AccountTestTokenMissing = "Screen.Account.TestTokenMissing";

        /// <summary>Test impossible sans ressaisie du jeton.</summary>
        public const string AccountTestTokenNeeded = "Screen.Account.TestTokenNeeded";

        /// <summary>Délai dépassé au test de connexion.</summary>
        public const string AccountTestTimeout = "Screen.Account.TestTimeout";

        /// <summary>Compte incomplet.</summary>
        public const string AccountInvalid = "Screen.Account.Invalid";

        /// <summary>Avertissement de changement de forge.</summary>
        public const string AccountForgeChanged = "Screen.Account.ForgeChanged";

        /// <summary>Titre de la fenêtre d'activité.</summary>
        public const string ActivityTitle = "Screen.Activity.Title";

        /// <summary>Colonne « heure ».</summary>
        public const string ActivityColumnTime = "Screen.Activity.Column.Time";

        /// <summary>Colonne « type ».</summary>
        public const string ActivityColumnKind = "Screen.Activity.Column.Kind";

        /// <summary>Colonne « pull request ».</summary>
        public const string ActivityColumnSubject = "Screen.Activity.Column.Subject";

        /// <summary>Colonne « détail ».</summary>
        public const string ActivityColumnDetail = "Screen.Activity.Column.Detail";

        /// <summary>Liste d'activité vide.</summary>
        public const string ActivityEmpty = "Screen.Activity.Empty";

        /// <summary>Bouton d'ouverture dans le navigateur.</summary>
        public const string ActivityOpen = "Screen.Activity.Open";

        /// <summary>Détail d'une ligne d'activité.</summary>
        public const string ActivityDetail = "Screen.Activity.Detail";

        /// <summary>Info-bulle de l'icône : nom et identité.</summary>
        public const string TrayViewer = "Screen.Tray.Viewer";

        /// <summary>Info-bulle de l'icône, sur plusieurs lignes.</summary>
        public const string TrayTooltip = "Screen.Tray.Tooltip";

        /// <summary>Configuration requise.</summary>
        public const string TrayNotConfigured = "Screen.Tray.NotConfigured";

        /// <summary>Erreur inattendue de cycle.</summary>
        public const string TrayError = "Screen.Tray.Error";

        /// <summary>Entrée « rafraîchir maintenant ».</summary>
        public const string MenuRefresh = "Screen.Menu.Refresh";

        /// <summary>Entrée « activité récente ».</summary>
        public const string MenuActivity = "Screen.Menu.Activity";

        /// <summary>Entrée « activité récente », avec des non-lus.</summary>
        public const string MenuActivityUnread = "Screen.Menu.ActivityUnread";

        /// <summary>Entrée « pull requests suivies ».</summary>
        public const string MenuPullRequests = "Screen.Menu.PullRequests";

        /// <summary>Aucune pull request active.</summary>
        public const string MenuNoPullRequest = "Screen.Menu.NoPullRequest";

        /// <summary>Entrée « pipelines ».</summary>
        public const string MenuPipelines = "Screen.Menu.Pipelines";

        /// <summary>Entrée « pipelines », avec des échecs.</summary>
        public const string MenuPipelinesFailing = "Screen.Menu.PipelinesFailing";

        /// <summary>Entrée « avertissements ».</summary>
        public const string MenuWarnings = "Screen.Menu.Warnings";

        /// <summary>Entrée « tout marquer comme lu ».</summary>
        public const string MenuMarkAllRead = "Screen.Menu.MarkAllRead";

        /// <summary>Entrée « paramètres ».</summary>
        public const string MenuSettings = "Screen.Menu.Settings";

        /// <summary>Entrée « quitter ».</summary>
        public const string MenuQuit = "Screen.Menu.Quit";

        /// <summary>Titre du toast de synthèse.</summary>
        public const string ToastSummaryTitle = "Screen.Toast.SummaryTitle";

        /// <summary>Attribution du toast de synthèse.</summary>
        public const string ToastSummaryHint = "Screen.Toast.SummaryHint";

        /// <summary>Titre de la bulle d'info de synthèse.</summary>
        public const string BalloonSummaryTitle = "Screen.Toast.BalloonSummaryTitle";

        /// <summary>Corps de la bulle d'info de synthèse.</summary>
        public const string BalloonSummaryBody = "Screen.Toast.BalloonSummaryBody";

        /// <summary>Erreur fatale au démarrage.</summary>
        public const string FatalError = "Screen.FatalError";
    }

    /// <summary>Libellés des réglages d'apparence et de langue.</summary>
    public static class Preference
    {
        /// <summary>Thème clair.</summary>
        public const string ThemeLight = "Preference.Theme.Light";

        /// <summary>Thème sombre.</summary>
        public const string ThemeDark = "Preference.Theme.Dark";

        /// <summary>Thème suivant Windows.</summary>
        public const string ThemeSystem = "Preference.Theme.System";

        /// <summary>Langue suivant Windows.</summary>
        public const string LanguageSystem = "Preference.Language.System";

        /// <summary>Français.</summary>
        public const string LanguageFrench = "Preference.Language.French";

        /// <summary>Anglais.</summary>
        public const string LanguageEnglish = "Preference.Language.English";
    }
}
