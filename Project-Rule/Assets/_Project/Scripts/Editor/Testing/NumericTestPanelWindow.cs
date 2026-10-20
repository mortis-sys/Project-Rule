using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RuleGame.Core.Rules;
using RuleGame.Development.Testing;
using RuleGame.Gameplay.Combat;
using RuleGame.Gameplay.Enemy;
using RuleGame.Gameplay.Player;
using RuleGame.Infrastructure;
using RuleGame.Infrastructure.Rules;

namespace RuleGame.Editor.Testing
{
    /// <summary>
    /// 数值测试面板：Boss 血量总量、规则数值（耗蓝 / 冷却 / 时长）、Play 中的实时数据。
    /// </summary>
    /// <remarks>
    /// <para>菜单 <c>Tools/规则游戏/数值测试面板</c>，可以停靠在任意位置；
    /// <b>编辑期就能用</b>，不需要进 Play。</para>
    /// <para>编辑期改的是资产（<see cref="EnemyDefinition"/> / <see cref="RuleDef"/>），
    /// 会走 Undo，<b>记得保存场景与资产</b>。实时血量、规则剩余时间、冷却只在 Play 中有值。</para>
    /// </remarks>
    public sealed class NumericTestPanelWindow : EditorWindow
    {
        private const float ScanInterval = 0.5f;

        private static readonly AttributeId[] AllAttributes = (AttributeId[])Enum.GetValues(typeof(AttributeId));
        private static readonly RelationId[] AllRelations = (RelationId[])Enum.GetValues(typeof(RelationId));

        private readonly List<EnemyActor> _bosses = new List<EnemyActor>();
        private readonly Dictionary<AttributeId, bool> _attributeFolds = new Dictionary<AttributeId, bool>();

        private RuleCatalog _catalog;
        private CombatSystem _combat;
        private Vector2 _scroll;
        private double _nextScan;
        private string _lastHit = "（还没有命中记录）";

        private bool _showBosses = true;
        private bool _showRuleDefs = true;
        private bool _showLive = true;

        [MenuItem("Tools/规则游戏/数值测试面板")]
        private static void Open()
        {
            var window = GetWindow<NumericTestPanelWindow>("数值测试");
            window.minSize = new Vector2(430f, 320f);
            window.Show();
        }

        private void OnEnable()
        {
            _catalog = null;   // 每次重建都重新挑目录，别缓存到只有重力的那份
            Rescan();
            BindCombat();
        }

        private void OnDisable()
        {
            if (_combat == null) return;
            _combat.OnDamageApplied -= OnDamage;
            _combat = null;
        }

        private void OnInspectorUpdate() => Repaint();

        // ==================================================================
        //  数据
        // ==================================================================

        private void RescanIfDue()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextScan) return;

            _nextScan = now + ScanInterval;
            Rescan();
            BindCombat();
        }

        private void Rescan()
        {
            _bosses.Clear();
            foreach (EnemyActor actor in FindObjectsByType<EnemyActor>(FindObjectsInactive.Include))
            {
                if (actor != null && actor.Definition != null && actor.Definition.IsBoss)
                    _bosses.Add(actor);
            }

            if (_catalog == null)
                _catalog = FindBestCatalog();
        }

        /// <summary>
        /// 挑规则最全的目录。
        /// </summary>
        /// <remarks>
        /// 工程里有三份 <c>RuleCatalog</c>：正式的 <c>Data/Rules/RuleCatalog.asset</c>（30 条），
        /// 以及对话实验室 / P4 重力 demo 各自那份（只有重力 5 条）。
        /// 直接取 <c>FindAssets</c> 的第一个会拿到只有重力的那份 —— 表现就是「只有重力能改」。
        /// </remarks>
        private static RuleCatalog FindBestCatalog()
        {
            RuleCatalog best = null;
            int bestCount = -1;
            string bestPath = "";

            foreach (string guid in AssetDatabase.FindAssets("t:RuleCatalog"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                RuleCatalog candidate = AssetDatabase.LoadAssetAtPath<RuleCatalog>(path);
                if (candidate == null) continue;

                int count = CountDefinitions(candidate);
                bool preferDataRules = path.Contains("/Data/Rules/") && !bestPath.Contains("/Data/Rules/");

                if (count > bestCount || (count == bestCount && preferDataRules))
                {
                    best = candidate;
                    bestCount = count;
                    bestPath = path;
                }
            }

            return best;
        }

        private static int CountDefinitions(RuleCatalog catalog)
        {
            int count = 0;
            foreach (AttributeId attribute in AllAttributes)
            {
                foreach (RelationId relation in AllRelations)
                {
                    if (catalog.Get(attribute, relation) != null) count++;
                }
            }
            return count;
        }

        private void BindCombat()
        {
            CombatSystem target = Application.isPlaying ? FindAnyObjectByType<CombatSystem>() : null;
            if (target == _combat) return;

            if (_combat != null) _combat.OnDamageApplied -= OnDamage;
            _combat = target;
            if (_combat != null) _combat.OnDamageApplied += OnDamage;
        }

        private void OnDamage(DamageInfo info)
        {
            string target = info.Target is Component component ? component.name : "（未知）";
            _lastHit = $"{target}  请求 {info.Amount:F1}  击退 {info.Knockback:F1}";
            Repaint();
        }

        // ==================================================================
        //  界面
        // ==================================================================

        private void OnGUI()
        {
            RescanIfDue();

            EditorGUILayout.HelpBox(Application.isPlaying
                ? "Play 中：实时血量 / 最近伤害 / 规则剩余时间可用。"
                : "编辑模式：改的是资产（EnemyDefinition / RuleDef），记得 Ctrl+S 保存。" +
                  "实时血量、规则剩余时间、冷却要在 Play 里才有。", MessageType.Info);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawBosses();
            DrawRuleDefs();
            DrawLive();

            EditorGUILayout.EndScrollView();
        }

        private void DrawBosses()
        {
            _showBosses = EditorGUILayout.Foldout(_showBosses, "Boss 血量", true);
            if (!_showBosses) return;

            if (_bosses.Count == 0)
            {
                EditorGUILayout.LabelField("场景里没有 Boss（需要 EnemyActor + EnemyDefinition.IsBoss）");
                return;
            }

            foreach (EnemyActor boss in _bosses)
                DrawBoss(boss);
        }

        private void DrawBoss(EnemyActor boss)
        {
            EnemyDefinition definition = boss.Definition;
            if (definition == null) return;

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(boss.name, EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            float total = EditorGUILayout.FloatField("血量总量（共享资产）", definition.Health);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(definition, "修改 Boss 血量总量");
                definition.Health = Mathf.Max(1f, total);
                EditorUtility.SetDirty(definition);
            }
            EditorGUILayout.LabelField("", $"阶段阈值 = 总量 ×2/3、×1/3（蝙蝠 / 闹钟 Boss）", EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            float damage = EditorGUILayout.FloatField("伤害（共享资产）", definition.Damage);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(definition, "修改 Boss 伤害");
                definition.Damage = Mathf.Max(0f, damage);
                EditorUtility.SetDirty(definition);
            }
            EditorGUILayout.LabelField("", "接触 / 近战 / 弹体 / 光束都读这个值（CombatSystem 里的基础伤害）",
                EditorStyles.miniLabel);

            BossHealthOverride overrideComponent = boss.GetComponent<BossHealthOverride>();
            if (overrideComponent != null)
            {
                EditorGUI.BeginChangeCheck();
                float value = EditorGUILayout.FloatField("场景覆盖（0 = 不改）", overrideComponent.Health);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(overrideComponent, "修改 Boss 血量覆盖");
                    overrideComponent.Health = Mathf.Max(0f, value);
                    EditorUtility.SetDirty(overrideComponent);
                    MarkSceneDirty(overrideComponent);
                }

                if (GUILayout.Button("移除场景覆盖组件"))
                    Undo.DestroyObjectImmediate(overrideComponent);
            }
            else if (GUILayout.Button("添加「场景血量覆盖」（只影响这个场景）"))
            {
                var added = Undo.AddComponent<BossHealthOverride>(boss.gameObject);
                added.Health = definition.Health;
                EditorUtility.SetDirty(added);
                MarkSceneDirty(added);
            }

            if (!Application.isPlaying || boss.Status == null) return;

            float max = Mathf.Max(1f, boss.Status.HealthMax);
            float health = boss.Status.Health;
            EditorGUILayout.LabelField("实时血量", $"{health:F1} / {max:F1}");

            float slider = EditorGUILayout.Slider(health, 0f, max);
            if (!Mathf.Approximately(slider, health))
                boss.Status.ApplyHealthDelta(slider - health);
        }

        private void DrawRuleDefs()
        {
            _showRuleDefs = EditorGUILayout.Foldout(_showRuleDefs, "规则数值（耗蓝 / 冷却 / 时长）", true);
            if (!_showRuleDefs) return;

            if (_catalog == null)
            {
                EditorGUILayout.HelpBox("找不到 RuleCatalog（Assets/_Project/Data/Rules/RuleCatalog.asset）",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("目录", AssetDatabase.GetAssetPath(_catalog), EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("关系", EditorStyles.miniBoldLabel, GUILayout.Width(84f));
            EditorGUILayout.LabelField("耗蓝", EditorStyles.miniBoldLabel, GUILayout.Width(56f));
            EditorGUILayout.LabelField("冷却", EditorStyles.miniBoldLabel, GUILayout.Width(56f));
            EditorGUILayout.LabelField("时长", EditorStyles.miniBoldLabel, GUILayout.Width(56f));
            EditorGUILayout.EndHorizontal();

            foreach (AttributeId attribute in AllAttributes)
            {
                bool open = _attributeFolds.TryGetValue(attribute, out bool value) && value;
                open = EditorGUILayout.Foldout(open, attribute.ToString(), true);
                _attributeFolds[attribute] = open;
                if (!open) continue;

                foreach (RelationId relation in AllRelations)
                {
                    RuleDef def = _catalog.Get(attribute, relation);
                    if (def == null) continue;

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(relation.ToString(), GUILayout.Width(84f));

                    EditorGUI.BeginChangeCheck();
                    float cost = EditorGUILayout.FloatField(def.ActivationCost, GUILayout.Width(56f));
                    float cooldown = EditorGUILayout.FloatField(def.Cooldown, GUILayout.Width(56f));
                    float duration = EditorGUILayout.FloatField(def.Duration, GUILayout.Width(56f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(def, "修改规则数值");
                        def.ActivationCost = Mathf.Max(0f, cost);
                        def.Cooldown = Mathf.Max(0f, cooldown);
                        def.Duration = duration;
                        EditorUtility.SetDirty(def);
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.LabelField("时长 = 0 表示关卡内永久。改动对之后释放的规则生效。",
                EditorStyles.miniLabel);
        }

        private void DrawLive()
        {
            _showLive = EditorGUILayout.Foldout(_showLive, "实时数据（Play）", true);
            if (!_showLive) return;

            if (!Application.isPlaying)
            {
                EditorGUILayout.LabelField("进 Play 后显示：规则剩余时间、最近伤害、玩家生命 / 能量 / 冷却。");
                return;
            }

            EditorGUILayout.LabelField("最近一击", _lastHit);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("--- 生效中的规则 ---", EditorStyles.boldLabel);

            RuleSystem rules = RuleSystem.Instance;
            if (rules == null)
            {
                EditorGUILayout.LabelField("RuleSystem 未初始化");
            }
            else
            {
                int count = 0;
                foreach (KeyValuePair<AttributeId, ActiveRule> pair in rules.Table.Slots)
                {
                    count++;
                    ActiveRule rule = pair.Value;
                    string time = rule.Remaining < 0f ? "永久" : $"{rule.Remaining:F1}s";
                    EditorGUILayout.LabelField($"{pair.Key} × {rule.Relation}", $"{time}  ({rule.Source})");
                }
                if (count == 0) EditorGUILayout.LabelField("（无）");
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("--- 玩家 ---", EditorStyles.boldLabel);

            EntityStatus player = FindPlayerStatus();
            if (player == null)
            {
                EditorGUILayout.LabelField("没找到玩家（PlayerController + EntityStatus）");
                return;
            }

            float maxHealth = Mathf.Max(1f, player.HealthMax);
            float health = EditorGUILayout.Slider($"生命 {player.Health:F1} / {player.HealthMax:F1}",
                player.Health, 0f, maxHealth);
            if (!Mathf.Approximately(health, player.Health))
                player.ApplyHealthDelta(health - player.Health);

            float maxEnergy = Mathf.Max(1f, player.EnergyMax);
            float energy = EditorGUILayout.Slider($"能量 {player.Energy:F1} / {player.EnergyMax:F1}",
                player.Energy, 0f, maxEnergy);
            if (!Mathf.Approximately(energy, player.Energy))
            {
                if (energy < player.Energy) player.TryConsumeEnergy(player.Energy - energy);
                else player.RestoreEnergy(energy - player.Energy);
            }

            foreach (AttributeId attribute in AllAttributes)
            {
                foreach (RelationId relation in AllRelations)
                {
                    float seconds = player.GetCooldownRemaining(attribute, relation);
                    if (seconds > 0f)
                        EditorGUILayout.LabelField($"{attribute} × {relation} 冷却", $"{seconds:F1}s");
                }
            }
        }

        // ==================================================================
        //  小工具
        // ==================================================================

        private static EntityStatus FindPlayerStatus()
        {
            PlayerController controller = FindAnyObjectByType<PlayerController>();
            return controller != null ? controller.GetComponentInChildren<EntityStatus>(true) : null;
        }

        private static void MarkSceneDirty(Component component)
        {
            if (component == null) return;

            var scene = component.gameObject.scene;
            if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
