using UnityEngine;
using RuleGame.Core.Rules;
using RuleGame.Gameplay.Common;
using RuleGame.Infrastructure.Common;
using RuleGame.Infrastructure.Rules;

namespace RuleGame.Editor.Rules
{
    /// <summary>
    /// 摩擦规则的端到端验证器。
    /// </summary>
    /// <remarks>
    /// <para>⚠️ 必须在 <b>Play 模式</b>下调用。</para>
    /// <para>重点验证的是<b>独占材质的生命周期</b>：共享材质不能就地改，
    /// 必须克隆、缩放、还原三步都干净。</para>
    /// </remarks>
    public static class FrictionRuleVerifier
    {
        private const float Tol = RuleVerifyHarness.Tolerance;
        private const float DefaultFriction = RuleVerifyHarness.UnityDefaultFriction;

        public static string Run()
        {
            var h = new RuleVerifyHarness();
            RuleDef[] defs = RuleVerifyHarness.MakeDefs(AttributeId.Friction, 10f);

            WorldRegistry.Initialize();
            RuleSystem.Initialize(new RuleVerifyHarness.TestCatalog(defs));
            RuleSystem sys = RuleSystem.Instance;
            if (sys == null) return "✗ RuleSystem 初始化失败";

            // ==============================================================
            //  ① 每个关系写进属性管理器的倍率
            // ==============================================================
            h.Section("1 规则表与属性管理器");

            // ⚠️ Revert 放第一个：它会把 FrictionInverted 置位，
            //    后面四个关系必须把它清掉 —— 这组断言就是在测「成对写」。
            (RelationId Rel, float Scale, bool Inverted)[] expect =
            {
                (RelationId.Revert,   FrictionRule.NormalScale,    true),
                (RelationId.Nullify,  FrictionRule.NullifiedScale, false),
                (RelationId.Constant, FrictionRule.NormalScale,    false),
                (RelationId.Enhance,  FrictionRule.EnhancedScale,  false),
                (RelationId.Weaken,   FrictionRule.WeakenedScale,  false),
            };

            foreach ((RelationId rel, float scale, bool inverted) in expect)
            {
                RuleRequestResult res = sys.Request(AttributeId.Friction, rel, RuleSource.Player);
                AttributeValues v = sys.Attributes.Values;
                h.Check($"{rel} → scale={scale}, inverted={inverted}",
                        res.Succeeded && Mathf.Abs(v.FrictionScale - scale) < Tol && v.FrictionInverted == inverted,
                        $"succeeded={res.Succeeded}, scale={v.FrictionScale}, inverted={v.FrictionInverted}");
            }

            // ==============================================================
            //  ② 实体应用：碰撞体没挂材质，走 Resources 兜底克隆
            // ==============================================================
            h.Section("2 实体应用（碰撞体没挂材质）");

            RuleSystem.Initialize(new RuleVerifyHarness.TestCatalog(defs));
            sys = RuleSystem.Instance;

            GameObject go = RuleVerifyHarness.SpawnBody("FrictionBox_NoMat", 1f, null,
                                                        out Rigidbody2D rb, out BoxCollider2D col, out RuleAffectedBody body);

            h.Check("实体已登记进 WorldRegistry", body.IsRegistered && RuleVerifyHarness.Contains(body),
                    $"IsRegistered={body.IsRegistered}, Count={WorldRegistry.Instance.Count}");
            h.Check($"基准摩擦 = Unity 默认值 {DefaultFriction}",
                    Mathf.Abs(body.BaseFriction - DefaultFriction) < Tol, body.BaseFriction.ToString("0.###"));
            h.Check("初始没有独占材质", !body.HasMaterialOverride, "已有");

            sys.Request(AttributeId.Friction, RelationId.Enhance, RuleSource.Player);
            h.Check("增强后拿到独占材质", body.HasMaterialOverride, "没有");
            h.Check($"增强后碰撞体摩擦 = {DefaultFriction * FrictionRule.EnhancedScale:0.###}",
                    col.sharedMaterial != null && Mathf.Abs(col.sharedMaterial.friction - DefaultFriction * FrictionRule.EnhancedScale) < Tol,
                    col.sharedMaterial == null ? "材质为 null" : col.sharedMaterial.friction.ToString("0.###"));

            sys.Request(AttributeId.Friction, RelationId.Nullify, RuleSource.Player);
            h.Check("失效后摩擦 = 0（冰面）",
                    col.sharedMaterial != null && Mathf.Abs(col.sharedMaterial.friction) < Tol,
                    col.sharedMaterial == null ? "材质为 null" : col.sharedMaterial.friction.ToString("0.###"));

            sys.Request(AttributeId.Friction, RelationId.Weaken, RuleSource.Player);
            h.Check($"削弱后碰撞体摩擦 = {DefaultFriction * FrictionRule.WeakenedScale:0.###}",
                    Mathf.Abs(col.sharedMaterial.friction - DefaultFriction * FrictionRule.WeakenedScale) < Tol,
                    col.sharedMaterial.friction.ToString("0.###"));

            sys.Request(AttributeId.Friction, RelationId.Constant, RuleSource.Player);
            h.Check("恒定（倍率 1）后释放独占材质", !body.HasMaterialOverride, "还在");
            h.Check("碰撞体材质还原为原来的 null", col.sharedMaterial == null,
                    col.sharedMaterial == null ? "" : col.sharedMaterial.name);

            // 反转只置标志位，不该碰物理材质
            sys.Request(AttributeId.Friction, RelationId.Revert, RuleSource.Player);
            h.Check("反转把 FrictionInverted 置位", sys.Attributes.Values.FrictionInverted, "false");
            h.Check("反转不动物理摩擦系数（倍率仍是 1，不该多出材质）",
                    !body.HasMaterialOverride, "多出了独占材质");

            // ==============================================================
            //  ③ 实体应用：碰撞体挂了 0.8 的材质，基准必须取自它
            // ==============================================================
            h.Section("3 实体应用（碰撞体挂了 0.8 的材质）");

            PhysicsMaterial2D src = RuleVerifyHarness.MakeMaterial(0.8f);
            GameObject go2 = RuleVerifyHarness.SpawnBody("FrictionBox_Mat", 1f, src,
                                                         out Rigidbody2D rb2, out BoxCollider2D col2, out RuleAffectedBody body2);

            h.Check("基准摩擦取自碰撞体材质 = 0.8",
                    Mathf.Abs(body2.BaseFriction - 0.8f) < Tol, body2.BaseFriction.ToString("0.###"));

            sys.Request(AttributeId.Friction, RelationId.Enhance, RuleSource.Player);
            h.Check("增强后摩擦 = 2.4",
                    col2.sharedMaterial != null && Mathf.Abs(col2.sharedMaterial.friction - 2.4f) < Tol,
                    col2.sharedMaterial == null ? "材质为 null" : col2.sharedMaterial.friction.ToString("0.###"));
            h.Check("独占材质是克隆件，不是原材质（否则会污染别人）",
                    !ReferenceEquals(col2.sharedMaterial, src),
                    "碰撞体指向了原材质");

            // 反转只置标志位：倍率回到 1，独占材质必须释放
            sys.Request(AttributeId.Friction, RelationId.Revert, RuleSource.Player);
            h.Check("反转置位 FrictionInverted", sys.Attributes.Values.FrictionInverted, "false");
            h.Check("反转把倍率带回 1，独占材质释放", !body2.HasMaterialOverride, "还在");
            h.Check("反转后碰撞体材质还原为原材质", ReferenceEquals(col2.sharedMaterial, src),
                    col2.sharedMaterial == null ? "null" : col2.sharedMaterial.name);

            sys.ClearAll();
            h.Check("ClearAll 后 FrictionInverted 被清掉", !sys.Attributes.Values.FrictionInverted, "还是 true");
            h.Check("ClearAll 后属性值回默认",
                    Mathf.Abs(sys.Attributes.Values.FrictionScale - 1f) < Tol,
                    sys.Attributes.Values.FrictionScale.ToString("0.###"));

            // ==============================================================
            //  ④ 生命周期
            // ==============================================================
            h.Section("4 生命周期");

            Object.DestroyImmediate(go);
            h.Check("销毁后从注册表剔除", !RuleVerifyHarness.Contains(body),
                    $"Count={WorldRegistry.Instance.Count}");

            Object.DestroyImmediate(go2);
            Object.DestroyImmediate(src);
            foreach (RuleDef d in defs) Object.DestroyImmediate(d);
            RuleSystem.Reset();

            return h.Report("摩擦规则验证");
        }
    }
}
