using UnityEngine;
using RuleGame.Core.Rules;
using RuleGame.Gameplay.Common;
using RuleGame.Infrastructure.Common;
using RuleGame.Infrastructure.Rules;

namespace RuleGame.Editor.Rules
{
    /// <summary>
    /// 质量规则的端到端验证器。
    /// </summary>
    /// <remarks>
    /// <para>⚠️ 必须在 <b>Play 模式</b>下调用。</para>
    /// <para>重点验证两件事：五个关系写进属性管理器的值，
    /// 以及 <c>RuleAffectedBody</c> 把它们落到 <c>Rigidbody2D.mass</c>。</para>
    /// </remarks>
    public static class MassRuleVerifier
    {
        /// <summary>基准质量取 4，让「反转取倒数」的结果和别的分支区分得开。</summary>
        private const float BaseMass = 4f;

        private const float Tol = RuleVerifyHarness.Tolerance;

        public static string Run()
        {
            var h = new RuleVerifyHarness();
            RuleDef[] defs = RuleVerifyHarness.MakeDefs(AttributeId.Mass, 10f);

            WorldRegistry.Initialize();
            RuleSystem.Initialize(new RuleVerifyHarness.TestCatalog(defs));
            RuleSystem sys = RuleSystem.Instance;
            if (sys == null) return "✗ RuleSystem 初始化失败";

            // ==============================================================
            //  ① 每个关系写进属性管理器的值
            // ==============================================================
            h.Section("1 规则表与属性管理器");

            (RelationId Rel, float Scale, bool Inverted)[] expect =
            {
                (RelationId.Revert,   MassRule.NormalScale,    true),
                (RelationId.Nullify,  MassRule.NullifiedScale, false),
                (RelationId.Constant, MassRule.NormalScale,    false),
                (RelationId.Enhance,  MassRule.EnhancedScale,  false),
                (RelationId.Weaken,   MassRule.WeakenedScale,  false),
            };

            foreach ((RelationId rel, float scale, bool inverted) in expect)
            {
                RuleRequestResult res = sys.Request(AttributeId.Mass, rel, RuleSource.Player);
                AttributeValues v = sys.Attributes.Values;
                h.Check($"{rel} → scale={scale}, inverted={inverted}",
                        res.Succeeded && Mathf.Abs(v.MassScale - scale) < Tol && v.MassInverted == inverted,
                        $"succeeded={res.Succeeded}, scale={v.MassScale}, inverted={v.MassInverted}");
            }

            ActiveRule? slot = sys.Table.GetSlot(AttributeId.Mass);
            h.Check("规则表槽位记录最后一次请求（Weaken）",
                    slot.HasValue && slot.Value.Relation == RelationId.Weaken,
                    slot.HasValue ? slot.Value.Relation.ToString() : "(空)");

            // ==============================================================
            //  ② 永久时长：Duration <= 0 必须归一成永久，不能第一帧就到期
            // ==============================================================
            h.Section("2 永久时长归一（RuleDef.Duration = 0）");

            RuleDef permDef = RuleVerifyHarness.MakeDef(AttributeId.Mass, RelationId.Enhance, 0f);
            RuleSystem.Initialize(new RuleVerifyHarness.TestCatalog(new[] { permDef }));
            sys = RuleSystem.Instance;
            sys.Request(AttributeId.Mass, RelationId.Enhance, RuleSource.Player);

            ActiveRule? ps = sys.Table.GetSlot(AttributeId.Mass);
            h.Check("Duration=0 归一成负数哨兵（永久）",
                    ps.HasValue && ps.Value.Remaining < 0f,
                    ps.HasValue ? $"Remaining={ps.Value.Remaining}" : "(空)");

            for (int i = 0; i < 5; i++) sys.Tick(1f);
            h.Check("模拟跑 5 秒后仍未到期", sys.Table.IsActive(AttributeId.Mass),
                    sys.Table.IsActive(AttributeId.Mass) ? "" : "已到期");

            // ==============================================================
            //  ③ 实体应用：属性值落到 Rigidbody2D.mass
            // ==============================================================
            h.Section("3 RuleAffectedBody 应用质量");

            RuleSystem.Initialize(new RuleVerifyHarness.TestCatalog(defs));
            sys = RuleSystem.Instance;

            GameObject go = RuleVerifyHarness.SpawnBody("MassVerifyBox", BaseMass, null,
                                                        out Rigidbody2D rb, out BoxCollider2D col, out RuleAffectedBody body);

            h.Check("实体已登记进 WorldRegistry", body.IsRegistered && RuleVerifyHarness.Contains(body),
                    $"IsRegistered={body.IsRegistered}, Count={WorldRegistry.Instance.Count}");
            h.Check("Colliders 不是 null", body.Colliders != null, "null");
            h.Check($"基准质量记录为 {BaseMass}", Mathf.Abs(body.BaseMass - BaseMass) < Tol,
                    body.BaseMass.ToString("0.###"));

            (RelationId Rel, float Expected)[] massCases =
            {
                (RelationId.Revert,   1f / BaseMass),
                (RelationId.Nullify,  BaseMass * MassRule.NullifiedScale),
                (RelationId.Constant, BaseMass),
                (RelationId.Enhance,  BaseMass * MassRule.EnhancedScale),
                (RelationId.Weaken,   BaseMass * MassRule.WeakenedScale),
            };

            foreach ((RelationId rel, float expected) in massCases)
            {
                sys.Request(AttributeId.Mass, rel, RuleSource.Player);
                h.Check($"{rel} → 刚体质量 {expected:0.###}",
                        Mathf.Abs(rb.mass - expected) < Tol, rb.mass.ToString("0.###"));
            }

            sys.ClearAll();
            h.Check($"ClearAll 后质量还原到 {BaseMass}", Mathf.Abs(rb.mass - BaseMass) < Tol,
                    rb.mass.ToString("0.###"));
            AttributeValues dv = sys.Attributes.Values;
            h.Check("ClearAll 后属性值回到默认",
                    Mathf.Abs(dv.MassScale - 1f) < Tol && !dv.MassInverted,
                    $"scale={dv.MassScale}, inverted={dv.MassInverted}");

            // ==============================================================
            //  ④ 生命周期
            // ==============================================================
            h.Section("4 生命周期");

            Object.DestroyImmediate(go);
            h.Check("销毁后从注册表剔除", !RuleVerifyHarness.Contains(body),
                    $"Count={WorldRegistry.Instance.Count}");

            foreach (RuleDef d in defs) Object.DestroyImmediate(d);
            Object.DestroyImmediate(permDef);
            RuleSystem.Reset();

            return h.Report("质量规则验证");
        }
    }
}
