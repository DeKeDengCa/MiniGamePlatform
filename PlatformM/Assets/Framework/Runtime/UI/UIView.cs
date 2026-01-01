using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Astorise.Framework.UI
{
    /// <summary>
    /// UI 视图基类，管理已加载的 GameObject 并通过反射绑定按钮回调。
    /// </summary>
    public class UIView
    {
        private const int DefaultItemCapacity = 64;

        private readonly Dictionary<string, GameObject> _itemMap = new Dictionary<string, GameObject>(DefaultItemCapacity);
        private GameObject _rootGameObject;
        private Transform _rootTransform;

        /// <summary>
        /// 实例化的 UI 根 GameObject
        /// </summary>
        public GameObject RootGameObject => _rootGameObject;

        /// <summary>
        /// 缓存的根 Transform
        /// </summary>
        public Transform RootTransform => _rootTransform;

        /// <summary>
        /// 使用根 GameObject 初始化视图并构建项目映射表。
        /// </summary>
        /// <param name="rootObject">实例化的 UI 根 GameObject</param>
        public void Initialize(GameObject rootObject)
        {
            _rootGameObject = rootObject;
            _rootTransform = rootObject != null ? rootObject.transform : null;

            if (_rootTransform == null)
            {
#if UNITY_DEBUG
                Debug.LogError("[UIView] Initialize failed: root GameObject is null");
#endif
                return;
            }

            InitItemName();
        }

        /// <summary>
        /// 通过遍历根 GameObject 的子对象构建项目映射表。
        /// </summary>
        public void InitItemName()
        {
            if (_rootTransform == null)
            {
#if UNITY_DEBUG
                Debug.LogError("[UIView] InitItemName failed: root transform is null");
#endif
                return;
            }

            _itemMap.Clear();
            TraverseAndMapItems(_rootTransform, _itemMap);

#if UNITY_DEBUG && UI_MANAGER
            Debug.Log($"[UIView] InitItemName completed, mapped {_itemMap.Count} items");
#endif
        }

        /// <summary>
        /// 根据名称获取项目 GameObject。
        /// </summary>
        /// <param name="itemName">项目名称</param>
        /// <returns>项目 GameObject，如果未找到则返回 null</returns>
        protected GameObject GetItem(string itemName)
        {
            if (string.IsNullOrEmpty(itemName))
            {
                return null;
            }

            _itemMap.TryGetValue(itemName, out GameObject item);
            return item;
        }

        /// <summary>
        /// 通过查找视图中的 On{ButtonName}Click 方法绑定按钮点击事件。
        /// </summary>
        public void BindButtons()
        {
            if (_rootTransform == null)
            {
#if UNITY_DEBUG
                Debug.LogError("[UIView] BindButtons failed: root transform is null");
#endif
                return;
            }

            if (_itemMap.Count == 0)
            {
#if UNITY_DEBUG
                Debug.LogError("[UIView] BindButtons failed: item map is empty, call InitItemName first");
#endif
                return;
            }

#if UNITY_DEBUG
            HashSet<string> buttonNames = new HashSet<string>();
            List<string> duplicateButtons = new List<string>();
#endif

            foreach (KeyValuePair<string, GameObject> kvp in _itemMap)
            {
                Button button = kvp.Value.GetComponent<Button>();
                if (button == null)
                {
                    continue;
                }

                string buttonName = kvp.Key;
#if UNITY_DEBUG
                if (buttonNames.Contains(buttonName))
                {
                    duplicateButtons.Add(buttonName);
                    continue;
                }

                buttonNames.Add(buttonName);
#endif

                string methodName = $"On{buttonName}Click";
                System.Reflection.MethodInfo method = GetType().GetMethod(
                    methodName,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (method != null)
                {
                    button.onClick.AddListener(() =>
                    {
                        try
                        {
                            method.Invoke(this, null);
                        }
                        catch (System.Exception ex)
                        {
#if UNITY_DEBUG
                            Debug.LogError($"[UIView] Button click handler failed: {buttonName}, error: {ex.Message}");
#endif
                        }
                    });

#if UNITY_DEBUG && UI_MANAGER
                    Debug.Log($"[UIView] Button bound: {buttonName} -> {methodName}");
#endif
                }
            }

#if UNITY_DEBUG
            if (duplicateButtons.Count > 0)
            {
#if UNITY_DEBUG
                Debug.LogError($"[UIView] Button names must be unique: {string.Join(", ", duplicateButtons)}");
#endif
            }
#endif
        }

        /// <summary>
        /// 处理路由消息。在派生视图中重写此方法。
        /// </summary>
        /// <param name="messageID">消息 ID</param>
        /// <param name="data">消息数据</param>
        public virtual void OnMessage(int messageID, object data)
        {
        }

        private void TraverseAndMapItems(Transform parent, Dictionary<string, GameObject> map)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                string childName = child.name;

                if (!string.IsNullOrEmpty(childName) && !map.ContainsKey(childName))
                {
                    map[childName] = child.gameObject;
                }

                TraverseAndMapItems(child, map);
            }
        }
    }
}
