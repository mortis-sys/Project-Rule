using System.Collections.Generic;
using UnityEngine;
using RuleGame.Core.Rules;
using RuleGame.Infrastructure.Common;
using RuleGame.Infrastructure.Rules;

namespace RuleGame.Editor.Rules
{
    /// <summary>
    /// 伤害规则的验证器。
    /// </summary>
    /// <remarks>
    /// <para>伤害不需要实体侧配合（没有物理载体），所以只验属性管理器的值。</para>
    /// <para>重点有两块：<b>三个标志位互不残留</b>，以及<b>挂账的结算触发点</b>——
    /// 后者是交给伤害处理模块的契约，漏了它账本会在覆盖路径上悄悄泄漏。</para>
    /// <para>⚠️ 必须在 <b>Play 模式</b>下调用（与其它验证器一致）。</para>
    /// </remarks>
    public static class DamageRuleVerifier
    {
        private const float Tol = RuleVerifyHarness.Tolerance;

        public static string Run()
        {
            var h = new RuleVerifyHarness();
            RuleDef[] defs = RuleVerifyHarness.MakeDefs(AttributeId.Damage, 10f);

            WorldRegistry.Initialize();
            RuleSystem.Initialize(new RuleVerifyHarness.TestCatalog(defs));
            RuleSystem sys = RuleSystem.Instance;
            if (sys == null) return "✗ RuleSystem 初始化失败";

            // ==============================================================
            //  ① 每个关系写全四个字段，标志位互不残留
            // ==============================================================
            h.Section("1 每个关系写进属性管理器的值");

            // 顺序刻意打乱：反转 / 失效 / 恒定 各占一个标志位，
            // 每一条都必须把前一条留下的标志位清掉。
            (RelationId Rel, float Scale, bool Inv, bool Dis, bool Def)[] expect =
            {
                (RelationId.Revert,   1f,                       true,  false, false),
                (RelationId.Nullify,  1f,                       false, true,  false),
                (RelationId.Constant, 1f,                       false, false, true),
                (RelationId.Enhance,  DamageRule.EnhancedScale, false, false, false),
                (RelationId.Weaken,   DamageRule.WeakenedScale, false, false, false),
                (RelationId.Constant, 1f,                       false, false, true),
            };

            foreach ((RelationId rel, float scale, bool inv, bool dis, bool def) in expect)
            {
                RuleRequestResult res = sys.Request(AttributeId.Damage, rel, RuleSource.Player);
                AttributeValues v = sys.Attributes.Values;
                bool ok = res.Succeeded
                          && Mathf.Abs(v.DamageScale - scale) < Tol
                          && v.DamageInverted == inv
                          && v.DamageDisabled == dis
                          && v.DamageDeferred == def;
                h.Check($"{rel} → scale={scale}, 反转={inv}, 失效={dis}, 挂账={def}", ok,
                        $"succeeded={res.Succeeded}, scale={v.DamageScale}, " +
                        $"反转={v.DamageInverted}, 失效={v.DamageDisabled}, 挂账={v.DamageDeferred}");
            }

            // ==============================================================
            //  ② 挂账的结算触发点（交给伤害处理模块的契约）
            // ==============================================================
            h.Section("2 挂账的结算触发点");

            var expired = new List<AttributeId>();
            sys.OnRuleExpired += a => expired.Add(a);

            sys.Request(AttributeId.Damage, RelationId.Constant, RuleSource.Player);
            h.Check("恒定置位 DamageDeferred",
                    sys.Attributes.Values.DamageDeferred,
                    sys.Attributes.Values.DamageDeferred.ToString());

            expired.Clear();
            sys.Request(AttributeId.Damage, RelationId.Enhance, RuleSource.Player);
            h.Check("恒定被增强覆盖时触发 OnRuleExpired(Damage) —— 这就是结算时机",
                    expired.Count == 1 && expired[0] == AttributeId.Damage,
                    $"expired=[{string.Join(",", expired)}]");
            h.Check("覆盖后挂账标志被清掉",
                    !sys.Attributes.Values.DamageDeferred,
                    sys.Attributes.Values.DamageDeferred.ToString());

            // ==============================================================
            //  ③ ClearAll 也要能收尾
            // ==============================================================
            h.Section("3 ClearAll");

            sys.Request(AttributeId.Damage, RelationId.Constant, RuleSource.Player);
            expired.Clear();
            sys.ClearAll();

            h.Check("ClearAll 触发 OnRuleExpired(Damage)",
                    expired.Count == 1 && expired[0] == AttributeId.Damage,
                    $"expired=[{string.Join(",", expired)}]");

            AttributeValues dv = sys.Attributes.Values;
            h.Check("ClearAll 后挂账标志清掉", !dv.DamageDeferred, dv.DamageDeferred.ToString());
            h.Check("ClearAll 后伤害参数回默认",
                    Mathf.Abs(dv.DamageScale - 1f) < Tol && !dv.DamageInverted && !dv.DamageDisabled,
                    $"scale={dv.DamageScale}, 反转={dv.DamageInverted}, 失效={dv.DamageDisabled}");

            RuleSystem.Reset();
            foreach (RuleDef d in defs) Object.DestroyImmediate(d);

            return h.Report("伤害规则验证");
        }
    }
}
