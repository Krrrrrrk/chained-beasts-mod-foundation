using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Logging;
using I2.Loc;

namespace ChainedBeasts.ModFoundation;

internal enum CustomText
{
    BulkUpName,
    BulkUpDescription,
    RunnersWrapsName,
    RunnersWrapsDescription,
    LeadfootName,
    LeadfootThreat,
    LeadfootDescription,
    AdrenalGraftName,
    AdrenalGraftDescription
}

/// <summary>
/// Owns translations and registers genuine runtime I2 terms. The term
/// registration prevents pooled Localize components from rejecting a custom
/// key and retaining the previous card or reveal text.
/// </summary>
internal static class CustomTranslations
{
    // Order: English, Korean, Japanese, Portuguese (Brazil), Spanish (Latin
    // Americas), German, French, Italian, Chinese (Simplified), Chinese
    // (Traditional), Russian, Turkish.
    private static readonly Dictionary<CustomText, string[]> Text = new()
    {
        [CustomText.BulkUpName] = new[]
        {
            "Bulk Up", "벌크 업", "バルクアップ", "Hipertrofia", "Fortalecimiento", "Masseaufbau",
            "Prise de masse", "Massa muscolare", "强壮体魄", "強健體魄", "Набор массы", "Kaslanma"
        },
        [CustomText.BulkUpDescription] = new[]
        {
            "Doubles your maximum and current health.",
            "최대 체력과 현재 체력이 두 배가 됩니다.",
            "最大体力と現在体力が2倍になります。",
            "Dobra sua vida máxima e atual.",
            "Duplica tu salud máxima y actual.",
            "Verdoppelt deine maximale und aktuelle Gesundheit.",
            "Double votre santé maximale et actuelle.",
            "Raddoppia la salute massima e attuale.",
            "使你的最大生命值和当前生命值翻倍。",
            "使你的最大生命值和目前生命值翻倍。",
            "Удваивает максимальное и текущее здоровье.",
            "Maksimum ve mevcut sağlığını iki katına çıkarır."
        },
        [CustomText.RunnersWrapsName] = new[]
        {
            "Runner's Wraps", "러너의 붕대", "ランナーの巻き布", "Faixas do Corredor", "Vendas del Corredor",
            "Läuferwickel", "Bandages du coureur", "Bende del corridore", "疾跑者裹布", "疾跑者裹布",
            "Обмотки бегуна", "Koşucu Sargıları"
        },
        [CustomText.RunnersWrapsDescription] = new[]
        {
            "Dodges use 25% less stamina while equipped.",
            "장착 시 회피에 소모되는 스태미나가 25% 감소합니다.",
            "装備中、回避で消費するスタミナが25%減少します。",
            "Esquivas consomem 25% menos vigor enquanto equipadas.",
            "Las esquivas consumen un 25% menos de aguante mientras están equipadas.",
            "Ausweichmanöver verbrauchen beim Tragen 25 % weniger Ausdauer.",
            "Les esquives consomment 25 % d'endurance en moins lorsque cet objet est équipé.",
            "Le schivate consumano il 25% di vigore in meno mentre sono equipaggiate.",
            "装备时，闪避消耗的耐力减少25%。",
            "裝備時，閃避消耗的耐力減少25%。",
            "При экипировке уклонения расходуют на 25% меньше выносливости.",
            "Kuşanıldığında kaçınmalar %25 daha az dayanıklılık harcar."
        },
        [CustomText.LeadfootName] = new[]
        {
            "Leadfoot", "무거운 발", "鉛の足", "Pé de Chumbo", "Pies de Plomo", "Bleifuß",
            "Pied de plomb", "Piede di piombo", "铅足", "鉛足", "Свинцовые ноги", "Kurşun Ayak"
        },
        [CustomText.LeadfootThreat] = new[]
        {
            "slow you down", "이동 속도를 낮춤", "移動速度を低下させる", "deixar você mais lento", "ralentizarte",
            "dich verlangsamen", "vous ralentir", "rallentarti", "使你减速", "使你減速", "замедлить вас",
            "seni yavaşlatmak"
        },
        [CustomText.LeadfootDescription] = new[]
        {
            "Movement speed reduced by {0}%.",
            "이동 속도가 {0}% 감소합니다.",
            "移動速度が{0}%低下します。",
            "Velocidade de movimento reduzida em {0}%.",
            "La velocidad de movimiento se reduce un {0}%.",
            "Bewegungsgeschwindigkeit um {0} % verringert.",
            "Vitesse de déplacement réduite de {0} %.",
            "Velocità di movimento ridotta del {0}%.",
            "移动速度降低{0}%。",
            "移動速度降低{0}%。",
            "Скорость передвижения снижена на {0}%.",
            "Hareket hızı %{0} azalır."
        },
        [CustomText.AdrenalGraftName] = new[]
        {
            "Adrenal Graft", "부신 이식", "副腎移植", "Enxerto Adrenal", "Injerto Suprarrenal",
            "Nebennierentransplantat", "Greffe surrénalienne", "Innesto surrenale", "肾上腺移植", "腎上腺移植",
            "Трансплантат надпочечника", "Adrenal Greft"
        },
        [CustomText.AdrenalGraftDescription] = new[]
        {
            "Stamina regenerates 25% faster for the rest of this run.",
            "이번 런이 끝날 때까지 스태미나 회복 속도가 25% 증가합니다.",
            "このランの間、スタミナの回復速度が25%上昇します。",
            "O vigor se regenera 25% mais rápido pelo restante desta partida.",
            "El aguante se regenera un 25% más rápido durante el resto de esta partida.",
            "Ausdauer regeneriert sich für den Rest dieses Durchlaufs 25 % schneller.",
            "L'endurance se régénère 25 % plus vite jusqu'à la fin de cette partie.",
            "Il vigore si rigenera il 25% più velocemente per il resto di questa partita.",
            "在本局剩余时间内，耐力恢复速度提高25%。",
            "在本局剩餘時間內，耐力恢復速度提高25%。",
            "До конца забега выносливость восстанавливается на 25% быстрее.",
            "Bu koşunun geri kalanında dayanıklılık %25 daha hızlı yenilenir."
        }
    };

    private sealed class TermDefinition
    {
        internal string Term;
        internal CustomText TextKey;
        internal int Severity;
    }

    private static ManualLogSource _logger;
    private static bool _bulkUpEnabled;
    private static bool _runnersWrapsEnabled;
    private static bool _leadfootEnabled;
    private static bool _adrenalGraftEnabled;
    private static bool _registered;

    internal static void Configure(
        ManualLogSource logger,
        bool bulkUpEnabled,
        bool runnersWrapsEnabled,
        bool leadfootEnabled,
        bool adrenalGraftEnabled)
    {
        _logger = logger;
        _bulkUpEnabled = bulkUpEnabled;
        _runnersWrapsEnabled = runnersWrapsEnabled;
        _leadfootEnabled = leadfootEnabled;
        _adrenalGraftEnabled = adrenalGraftEnabled;
        _registered = false;
    }

    internal static string Get(CustomText key, int severity = 0)
    {
        return GetForLanguage(key, LocalizationManager.CurrentLanguage, severity);
    }

    internal static void RegisterRuntimeTerms()
    {
        if (_registered)
        {
            return;
        }

        try
        {
            LocalizationManager.InitializeIfNeeded();
            var sources = LocalizationManager.Sources;
            LanguageSourceData source = null;
            var languageCount = -1;

            if (sources is not null)
            {
                for (var index = 0; index < sources.Count; index++)
                {
                    var candidate = sources[index];
                    var candidateCount = candidate?.mLanguages?.Count ?? 0;
                    if (candidateCount > languageCount)
                    {
                        source = candidate;
                        languageCount = candidateCount;
                    }
                }
            }

            if (source is null || source.mLanguages is null || source.mLanguages.Count == 0)
            {
                _logger?.LogWarning(
                    "[ChainedBeasts.ModFoundation] no initialized I2 source was available; localized lookup hooks remain active");
                return;
            }

            var definitions = BuildDefinitions();
            foreach (var definition in definitions)
            {
                var termData = source.GetTermData(definition.Term) ??
                               source.AddTerm(definition.Term, eTermType.Text, false);
                if (termData is null)
                {
                    throw new InvalidOperationException($"I2 failed to add term '{definition.Term}'.");
                }

                for (var languageIndex = 0; languageIndex < source.mLanguages.Count; languageIndex++)
                {
                    var language = source.mLanguages[languageIndex]?.Name;
                    termData.SetTranslation(
                        languageIndex,
                        GetForLanguage(definition.TextKey, language, definition.Severity));
                }
            }

            source.UpdateDictionary(true);
            _registered = true;
            _logger?.LogInfo(
                $"[ChainedBeasts.ModFoundation] registered {definitions.Count} runtime I2 terms across {source.mLanguages.Count} languages");
        }
        catch (Exception exception)
        {
            _logger?.LogError(
                $"[ChainedBeasts.ModFoundation] runtime I2 term registration failed: {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static List<TermDefinition> BuildDefinitions()
    {
        var definitions = new List<TermDefinition>();

        if (_bulkUpEnabled)
        {
            Add(definitions, CustomText.BulkUpName, 0,
                BulkUpFeature.BulkUpNameTerm, $"Skills/{CustomContentIds.BulkUpSkill}_Name");
            Add(definitions, CustomText.BulkUpDescription, 0,
                BulkUpFeature.BulkUpDescriptionTerm, $"Skills/{CustomContentIds.BulkUpSkill}_Desc");
        }

        if (_runnersWrapsEnabled)
        {
            Add(definitions, CustomText.RunnersWrapsName, 0,
                Equipment.RunnersWrapsFeature.NameTerm,
                $"Purchases/{CustomContentIds.RunnersWrapsPurchase}_Name", "Purchases/RunnersWraps_Name");
            Add(definitions, CustomText.RunnersWrapsDescription, 0,
                Equipment.RunnersWrapsFeature.DescriptionTerm,
                $"Purchases/{CustomContentIds.RunnersWrapsPurchase}_Desc", "Purchases/RunnersWraps_Desc");
        }

        if (_leadfootEnabled)
        {
            Add(definitions, CustomText.LeadfootName, 0,
                "ModFoundation/Leadfoot/Name",
                $"Injuries/{CustomContentIds.LeadfootInjury}_Name", "Injuries/Leadfoot_Name");
            Add(definitions, CustomText.LeadfootThreat, 0,
                "ModFoundation/Leadfoot/Threat",
                $"Injuries/{CustomContentIds.LeadfootInjury}_Threat", "Injuries/Leadfoot_Threat");
            for (var severity = 1; severity <= 3; severity++)
            {
                Add(definitions, CustomText.LeadfootDescription, severity,
                    $"ModFoundation/Leadfoot/Description/{severity}",
                    $"Injuries/{CustomContentIds.LeadfootInjury}_Desc_{severity}",
                    $"Injuries/Leadfoot_Desc_{severity}");
            }
        }

        if (_adrenalGraftEnabled)
        {
            Add(definitions, CustomText.AdrenalGraftName, 0,
                Surgeries.AdrenalGraftFeature.NameTerm,
                $"Surgeries/{CustomContentIds.AdrenalGraftSurgery}_Name", "Surgeries/AdrenalGraft_Name");
            Add(definitions, CustomText.AdrenalGraftDescription, 0,
                Surgeries.AdrenalGraftFeature.DescriptionTerm,
                $"Surgeries/{CustomContentIds.AdrenalGraftSurgery}_Desc", "Surgeries/AdrenalGraft_Desc");
        }

        return definitions;
    }

    private static void Add(List<TermDefinition> definitions, CustomText textKey, int severity, params string[] terms)
    {
        foreach (var term in terms)
        {
            definitions.Add(new TermDefinition { Term = term, TextKey = textKey, Severity = severity });
        }
    }

    private static string GetForLanguage(CustomText key, string language, int severity)
    {
        var translations = Text[key];
        var value = translations[GetLanguageIndex(language)];
        return severity > 0
            ? string.Format(CultureInfo.InvariantCulture, value, severity * 10)
            : value;
    }

    private static int GetLanguageIndex(string language)
    {
        if (string.IsNullOrWhiteSpace(language)) return 0;
        if (language.StartsWith("Korean", StringComparison.OrdinalIgnoreCase)) return 1;
        if (language.StartsWith("Japanese", StringComparison.OrdinalIgnoreCase)) return 2;
        if (language.StartsWith("Portuguese", StringComparison.OrdinalIgnoreCase)) return 3;
        if (language.StartsWith("Spanish", StringComparison.OrdinalIgnoreCase)) return 4;
        if (language.StartsWith("German", StringComparison.OrdinalIgnoreCase)) return 5;
        if (language.StartsWith("French", StringComparison.OrdinalIgnoreCase)) return 6;
        if (language.StartsWith("Italian", StringComparison.OrdinalIgnoreCase)) return 7;
        if (language.StartsWith("Chinese (Traditional)", StringComparison.OrdinalIgnoreCase)) return 9;
        if (language.StartsWith("Chinese", StringComparison.OrdinalIgnoreCase)) return 8;
        if (language.StartsWith("Russian", StringComparison.OrdinalIgnoreCase)) return 10;
        if (language.StartsWith("Turkish", StringComparison.OrdinalIgnoreCase)) return 11;
        return 0;
    }
}
