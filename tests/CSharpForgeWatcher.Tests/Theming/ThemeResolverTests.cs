using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Application.Text;
using CSharpForgeWatcher.Application.Theming;

namespace CSharpForgeWatcher.Tests.Theming;

/// <summary>SPEC-UI-THEME-001 et SPEC-UI-THEME-002 — les trois positions et leur résolution.</summary>
[TestFixture]
public sealed class ThemeResolverTests
{
    [Test]
    [Category("SPEC-UI-THEME-001")]
    public void Le_reglage_a_exactement_trois_positions()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ThemeResolver.All, Has.Count.EqualTo(3));
            Assert.That(ThemeResolver.All, Does.Contain(ThemePreference.System));
            Assert.That(ThemeResolver.All, Does.Contain(ThemePreference.Light));
            Assert.That(ThemeResolver.All, Does.Contain(ThemePreference.Dark));
        });
    }

    [Test]
    [Category("SPEC-UI-THEME-001")]
    public void Le_defaut_de_configuration_est_automatique()
    {
        Assert.That(new WatcherConfiguration().Theme, Is.EqualTo(ThemePreference.System));
    }

    [TestCase(ThemePreference.Light, false, EffectiveTheme.Light)]
    [TestCase(ThemePreference.Light, true, EffectiveTheme.Light)]
    [TestCase(ThemePreference.Dark, false, EffectiveTheme.Dark)]
    [TestCase(ThemePreference.Dark, true, EffectiveTheme.Dark)]
    [TestCase(ThemePreference.System, false, EffectiveTheme.Light)]
    [TestCase(ThemePreference.System, true, EffectiveTheme.Dark)]
    [Category("SPEC-UI-THEME-002")]
    public void La_resolution_suit_la_table_de_la_spec(
        ThemePreference preference,
        bool systemPrefersDark,
        EffectiveTheme expected)
    {
        Assert.That(ThemeResolver.Resolve(preference, systemPrefersDark), Is.EqualTo(expected));
    }

    [Test]
    [Category("SPEC-UI-THEME-002")]
    public void Une_apparence_systeme_illisible_retombe_sur_le_clair()
    {
        // La sonde répond false quand la clé de registre est absente : c'est le cas nominal
        // sur les versions anciennes de Windows.
        Assert.That(
            ThemeResolver.Resolve(ThemePreference.System, systemPrefersDark: false),
            Is.EqualTo(EffectiveTheme.Light));
    }

    [Test]
    [Category("SPEC-UI-THEME-001")]
    public void Chaque_position_a_un_libelle_dans_les_deux_langues()
    {
        foreach (var preference in ThemeResolver.All)
        {
            // La préférence porte une clé, jamais une formulation : c'est le catalogue qui
            // sait la dire, dans la langue courante (SPEC-UI-LANG-002).
            Assert.Multiple(() =>
            {
                Assert.That(TextCatalogue.For(EffectiveLanguage.French).Knows(preference.ToLabelKey()), Is.True);
                Assert.That(TextCatalogue.For(EffectiveLanguage.English).Knows(preference.ToLabelKey()), Is.True);
            });
        }
    }
}
