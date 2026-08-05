# Objet de la modification

<!-- En une ou deux phrases : quel besoin cette PR couvre-t-elle ? -->

## Vérifications avant relecture

- [ ] `dotnet build -c Release` passe sans avertissement
- [ ] `dotnet test` passe (tous les tests, y compris les nouveaux)
- [ ] `dotnet format --verify-no-changes` ne signale rien
- [ ] Specs / scénarios Gherkin de `docs/specs/` mis à jour si le comportement change
- [ ] `CHANGELOG.md` mis à jour
- [ ] Aucun secret commité (jeton, mot de passe, `config.json`, `state.json`)

## Points d'attention pour le relecteur

<!-- Choix discutables, dette assumée, zones à regarder en priorité. Supprimer si vide. -->
