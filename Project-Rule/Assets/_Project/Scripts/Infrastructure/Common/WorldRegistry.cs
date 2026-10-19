using System.Collections.Generic;
using System.Collections.ObjectModel;
using RuleGame.Infrastructure.Common;

namespace RuleGame.Infrastructure.Common
{
    /// <summary>
    /// <see cref="IWorldRegistry"/> 的默认实现：一个保持登记顺序的列表。
    /// </summary>
    /// <remarks>
    /// <para><b>为什么用 List 而不是 HashSet</b>：登记顺序要稳定，快照采集和规则遍历
    /// 才有确定的顺序。实体数量在几十的量级，线性查找的代价可以忽略。</para>
    /// <para><b>为什么要剔除已销毁的实体</b>：Unity 里对象可能在没走
    /// <c>OnDisable</c> 的情况下被销毁（例如场景卸载、<c>DestroyImmediate</c>）。
    /// 注册表留着一堆空引用，规则类一遍历就抛 <c>MissingReferenceException</c>。
    /// 这里做兜底清理，但**正常路径仍然要求实现者自己注销**。</para>
    /// </remarks>
    public sealed class WorldRegistry : IWorldRegistry
    {
        // ==================================================================
        //  单例
        // ==================================================================

        private static WorldRegistry _instance;

        /// <summary>
        /// 全局单例。
        /// </summary>
        /// <remarks>
        /// 与 <c>RuleSystem</c> 同样的模式：由启动器（<c>RuleSystemDriver</c>）创建，
        /// 场景销毁时 <see cref="Reset"/>。
        /// </remarks>
        public static WorldRegistry Instance => _instance;

        /// <summary>初始化单例。重复调用会覆盖（重开关卡 / 切场景时有用）。</summary>
        public static void Initialize() => _instance = new WorldRegistry();

        /// <summary>仅测试用：注入替身。</summary>
        public static void SetInstanceForTesting(WorldRegistry instance) => _instance = instance;

        /// <summary>清空单例。切场景 / 退出时调用，让下次重新初始化。</summary>
        public static void Reset() => _instance = null;

        // ==================================================================
        //  字段
        // ==================================================================

        private readonly List<IRuleAffected> _entities = new List<IRuleAffected>();
        private readonly ReadOnlyCollection<IRuleAffected> _view;

        public WorldRegistry()
        {
            // 只包一次。每次访问 All 都新建包装器会造成无谓的分配。
            _view = _entities.AsReadOnly();
        }

        /// <summary>已登记且未销毁的实体数量。</summary>
        public int Count
        {
            get
            {
                PruneDestroyed();
                return _entities.Count;
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<IRuleAffected> All
        {
            get
            {
                PruneDestroyed();
                return _view;
            }
        }

        /// <inheritdoc />
        public void Register(IRuleAffected entity)
        {
            if (entity == null) return;

            // 重复登记是常见 bug（OnEnable 被调两次、父物体和子物体都登记），
            // 静默去重比让规则类把同一个实体算两遍要好。
            if (_entities.Contains(entity)) return;

            _entities.Add(entity);
        }

        /// <inheritdoc />
        public void Unregister(IRuleAffected entity)
        {
            if (entity == null) return;
            _entities.Remove(entity);
        }

        /// <summary>清空登记表。重开关卡 / 加载存档时用。</summary>
        public void Clear() => _entities.Clear();

        /// <summary>
        /// 倒序扫描并移除已销毁的实体。
        /// </summary>
        /// <remarks>
        /// 倒序是为了让 <c>RemoveAt</c> 不影响尚未检查的下标。
        /// </remarks>
        private void PruneDestroyed()
        {
            for (int i = _entities.Count - 1; i >= 0; i--)
            {
                if (IsDestroyed(_entities[i])) _entities.RemoveAt(i);
            }
        }

        /// <summary>
        /// 实体是否已经失效。
        /// </summary>
        /// <remarks>
        /// <para>两个判据都要：</para>
        /// <list type="bullet">
        /// <item>实体本身被 GC 掉或置空 —— <c>entity == null</c></item>
        /// <item>实体还在但刚体被销毁 —— <c>Body == null</c>（Unity 重载了 ==，
        /// 销毁后的引用会等于 null）</item>
        /// </list>
        /// <para>⚠️ 这里**会调用 <c>entity.Body</c>**，所以要求实现者即使刚体被销毁
        /// 也不要让这个 getter 抛异常。字段直读即可，不要在里面做别的事。</para>
        /// </remarks>
        private static bool IsDestroyed(IRuleAffected entity)
        {
            if (entity == null) return true;
            return entity.Body == null;
        }
    }
}
