using System.Globalization;
using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Application.Text;

namespace CSharpForgeWatcher.Tests.Text;

/// <summary>SPEC-UI-LANG-001 — les trois positions du réglage de langue et leur résolution.</summary>
[TestFixture]
public sealed class LanguageResolverTests
{
    [Test]
    [Category("SPEC-UI-LANG-001")]
    public void Le_reglage_a_exactement_trois_positions()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LanguageResolver.All, Has.Count.EqualTo(3));
            Assert.That(LanguageResolver.All, Does.Contain(LanguagePreference.System));
            Assert.That(LanguageResolver.All, Does.Contain(LanguagePreference.French));
            Assert.That(LanguageResolver.All, Does.Contain(LanguagePreference.English));
        });
    }

    [Test]
    [Category("SPEC-UI-LANG-001")]
    public void Le_defaut_de_configuration_suit_Windows()
    {
        Assert.That(new WatcherConfiguration().Language, Is.EqualTo(LanguagePreference.System));
    }

    [TestCase(LanguagePreference.French, "en-US", EffectiveLanguage.French)]
    [TestCase(LanguagePreference.French, "fr-FR", EffectiveLanguage.French)]
    [TestCase(LanguagePreference.English, "fr-FR", EffectiveLanguage.English)]
    [TestCase(LanguagePreference.English, "en-GB", EffectiveLanguage.English)]
    [TestCase(LanguagePreference.System, "fr-FR", EffectiveLanguage.French)]
    [TestCase(LanguagePreference.System, "fr-CA", EffectiveLanguage.French)]
    [TestCase(LanguagePreference.System, "en-US", EffectiveLanguage.English)]
    [TestCase(LanguagePreference.System, "de-DE", EffectiveLanguage.English)]
    [Category("SPEC-UI-LANG-001")]
    public void La_resolution_suit_la_table_de_la_spec(
        LanguagePreference preference,
        string systemCulture,
        EffectiveLanguage expected)
    {
        Assert.That(
            LanguageResolver.Resolve(preference, CultureInfo.GetCultureInfo(systemCulture)),
            Is.EqualTo(expected));
    }

    [Test]
    [Category("SPEC-UI-LANG-001")]
    public void Une_langue_systeme_illisible_retombe_sur_l_anglais()
    {
        // Aucune culture connue : l'anglais est le repli le plus utile pour un poste dont on ne
        // sait rien, et le seul choix honnête tant que l'application ne connaît que deux langues.
        Assert.That(
            LanguageResolver.Resolve(LanguagePreference.System, systemCulture: null),
            Is.EqualTo(EffectiveLanguage.English));
    }

    [Test]
    [Category("SPEC-UI-LANG-001")]
    public void Chaque_position_a_un_libelle_dans_les_deux_langues()
    {
        foreach (var preference in LanguageResolver.All)
        {
            Assert.Multiple(() =>
            {
                Assert.That(TextCatalogue.For(EffectiveLanguage.French).Knows(preference.ToLabelKey()), Is.True);
                Assert.That(TextCatalogue.For(EffectiveLanguage.English).Knows(preference.ToLabelKey()), Is.True);
            });
        }
    }

    [Test]
    [Category("SPEC-UI-LANG-001")]
    public void Les_noms_de_langue_sont_ecrits_dans_leur_propre_langue()
    {
        // Quelqu'un coincé dans une interface qu'il ne lit pas doit reconnaître sa langue dans
        // la liste : « Français » reste « Français », même en anglais.
        Assert.Multiple(() =>
        {
            Assert.That(
                TextCatalogue.For(EffectiveLanguage.English).Get(LanguagePreference.French.ToLabelKey()),
                Is.EqualTo("Français"));
            Assert.That(
                TextCatalogue.For(EffectiveLanguage.French).Get(LanguagePreference.English.ToLabelKey()),
                Is.EqualTo("English"));
        });
    }
}
