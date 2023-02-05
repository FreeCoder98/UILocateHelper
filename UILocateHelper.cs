using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UIEditorTool
{
    //Call this function when the editor starts
    [InitializeOnLoad]
    public static class UILocateHelper
    {
        static UILocateHelper()
        {
            SceneView.onSceneGUIDelegate += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            var ec = Event.current;
            if (ec != null && ec.button == 1 && ec.type == EventType.MouseUp)
            {
                ec.Use();
                Vector2 mousePosition = Event.current.mousePosition;

                // The scale of GUI points relative to screen pixels for the current view
                float mult = EditorGUIUtility.pixelsPerPoint;
                mousePosition.y = sceneView.camera.pixelHeight - mousePosition.y * mult;
                mousePosition.x *= mult;

                // 1、Get all UI elements in the scene
                IEnumerable<RectTransform> elements = GetAllUIElementInScene();

                // 2、Filter the  UI elements in the scene
                var groups = elements
                    .Where(element => element.gameObject.activeInHierarchy)
                    .Where(element => IsValid(element))
                    .Where(element => RectTransformUtility.RectangleContainsScreenPoint(element, mousePosition, sceneView.camera))
                    .OrderByDescending(element => GetElementDepth(element))
                    .Take(8).GroupBy(element => element.gameObject.scene.name).ToArray();


                // 3、Draw Menu UI
                var dic = new Dictionary<string, int>();
                var gc = new GenericMenu();
                foreach (var group in groups)
                {
                    foreach (var element in group)
                    {
                        bool containsKey = dic.ContainsKey(element.name);
                        int count = containsKey ? dic[element.name]++ : 0;

                        AddLocateGoItem(gc, element, element.name, count, containsKey);
                        AddFindReferenceItem(gc, element, element.name, count, containsKey);

                        if (!containsKey)
                        {
                            dic.Add(element.name, 1);
                        }
                    }
                }
                
                gc.ShowAsContext();
            }

        }

        private static IEnumerable<RectTransform> GetAllUIElementInScene()
        {
            RectTransform[] rects = UnityEditor.SceneManagement.StageUtility.GetCurrentStageHandle().FindComponentsOfType<RectTransform>();
            for (int i = 0; i < rects.Length; i++)
            {
                yield return rects[i];
            }
        }


        #region Menu UI
        
        private static string MENU_ITEM_DESC_LOCATE = "Locate GameObject";
        private static string MENU_ITEM_DESC_FIND = "Find References";
        private static string MENU_ITEM_DESC_NODE = "Node";
        public static void AddLocateGoItem(GenericMenu gc, Transform target, string name, int count, bool isRepeat)
        {
            var text = isRepeat ? string.Format("{0}[{1}]/{2}", name, count.ToString(), MENU_ITEM_DESC_LOCATE) : string.Format( "{0}/{1}", name, MENU_ITEM_DESC_LOCATE);
            var content = new GUIContent(text);
            gc.AddItem(content, false, () =>
            {
                Selection.activeTransform = target;
                EditorGUIUtility.PingObject(target.gameObject);
            });
        }
        
        
        public static void AddFindReferenceItem(GenericMenu gc, Transform target, string name, int count, bool isRepeat, bool isHierarchy = false)
        {
            //1、find node reference
            string text = isHierarchy ? MENU_ITEM_DESC_NODE :
                          isRepeat ? string.Format("{0}[{1}]/{2}/{3}", name, count.ToString() , MENU_ITEM_DESC_FIND, MENU_ITEM_DESC_NODE) : 
                                     string.Format("{0}/{1}/{2}", name, MENU_ITEM_DESC_FIND , MENU_ITEM_DESC_NODE);
            var content = new GUIContent(text);
            
            gc.AddItem(content, false, () =>
            {
                FindReferenced(target.GetInstanceID());
            });
            
            //2、find scripts reference
            Component[] components = target.GetComponents<Component>();
            int instanceID = target.gameObject.GetInstanceID();
            
            for (int i = 0; i < components.Length; i++)
            {
                string scriptsName = components[i].GetType().FullName;
                //获取脚本名称
                int scriptID = components[i].GetInstanceID();
                
                string txt = isHierarchy ? scriptsName :
                             isRepeat ? string.Format("{0}[{1}]/{2}/{3}", name, count.ToString(), MENU_ITEM_DESC_FIND, scriptsName) : 
                                        string.Format("{0}/{1}/{2}", name, MENU_ITEM_DESC_FIND , scriptsName);
                
                var content1 = new GUIContent(txt);
                
                gc.AddItem(content1, false, () =>
                {
                    FindReferenced(scriptID,instanceID, name);
                });
            }
        }
        
        #endregion
       
        
        #region filter UI element 
        
        private static Dictionary<int, int> depthDic = new Dictionary<int, int>(2048);
       
        // Filter specified scripts based on project requirements
        private static List<string> filterNames = new List<string> {"canvas",};
        
        private static bool IsValid(RectTransform element)
        {
            return IsDepthValid(element) && IsElementValid(element);
        }
        
        private static bool IsDepthValid(RectTransform element)
        {
            if (element != null)
            {
                int instanceID = element.GetInstanceID();
                if (!depthDic.TryGetValue(instanceID, out int depth))
                {
                    depth = GetElementDepth(element);
                    depthDic[instanceID] = depth;
                }
                
                int targetDepth = Application.isPlaying ? 1 : 3;
                return depth > 0;
            }
            
            return false;
        }

        private static bool IsElementValid(RectTransform element)
        {
            if (element == null)
            {
                return false;
            }
            
            Component[] components = element.GetComponents<Component>();
            if (components == null || components.Length <= 1)
            {
                return false;
            }

            string nodeName = element.name.ToLower();
            for (int i = 0; i < filterNames.Count; i++)
            {
                if (nodeName.Contains(filterNames[i]))
                {
                    return false;
                }
            }

            // Filter specified scripts based on project requirements
            for (int i = 0; i < components.Length; i++)
            {
                // Filter specific scripts
            }
            
            return true;
        }
        
        public static int GetElementDepth(RectTransform element)
        {
            int depth = 0;
            
            if (element != null)
            {
                if (depthDic.TryGetValue(element.GetInstanceID(), out depth))
                {
                    return depth;
                }
            
                var transform = element.gameObject.transform;
                while (transform != null && transform.parent != null)
                {
                    depth++;
                    transform = transform.parent;
                }
            }
          
            return depth;
        }
        
        #endregion


        #region Find Reference
        private const string CANVAS_ENVIROMENT_ROOT = "Canvas (Environment)";
        private const string CANVAS_RUNTIME_ROOT = "CanvasParent";
        
        public static void FindReferenced(int instanceID)
        {
            var elements = UnityEditor.SceneManagement.StageUtility.GetCurrentStageHandle().FindComponentsOfType<Transform>();
            var rootElement = GetRootElement(elements);
            
            FindReferenced(rootElement,instanceID);
        }

        public static void FindReferenced(int scriptID, int goInstanceID, string name)
        {
            var elements = UnityEditor.SceneManagement.StageUtility.GetCurrentStageHandle().FindComponentsOfType<Transform>();
            var rootElement = GetRootElement(elements);
            
            FindReferencedByInstanceID(rootElement, scriptID, goInstanceID, name);
        }

        private static Transform GetRootElement(Transform[] elements)
        {
            foreach (var element in elements)
            {
                if (element != null && (element.name.Equals(CANVAS_ENVIROMENT_ROOT) || element.name.Equals(CANVAS_RUNTIME_ROOT)))
                {
                    return element;
                }
            }

            return null;
        }
        
        private static void FindReferenced(Transform element, int instanceID)
        {
            if (element == null)
            {
                return;
            }
            
            Component[] components = element.GetComponentsInChildren<Component>();
            
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    continue;
                }

                var fieldInfos = components[i].GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).ToList<FieldInfo>();
                
                for (int j = 0; j < fieldInfos.Count; j++)
                {
                    var fileInfo = fieldInfos[j];
                    var fileValue = fileInfo.GetValue(components[i]);
                    
                    if (fileValue == null)
                    {
                        continue;
                    }

                    GameObject go = fileValue as GameObject;
                    if (go != null)
                    {
                        int targetID = go.GetInstanceID();
                        if (targetID == instanceID)
                        {
                            Selection.activeTransform = components[i].transform;
                            EditorGUIUtility.PingObject(components[i]);
                        }
                    }
                }
            }
        }

        private static void FindReferencedByInstanceID(Transform element, int scriptID, int goInstanceID, string elementName)
        {
            if (element == null)
            {
                return;
            }
            
            Component[] components = element.GetComponentsInChildren<Component>();
            int instanceID = element.GetInstanceID();
            
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    continue;
                }

                var fieldInfos = components[i].GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).ToList<FieldInfo>();
                for (int j = 0; j < fieldInfos.Count; j++)
                {
                    var fileInfo = fieldInfos[j];
                    var fileValue = fileInfo.GetValue(components[i]);

                    if (fileValue == null)
                    {
                        continue;
                    }

                    if (typeof(Component).IsAssignableFrom(fileValue.GetType()))
                    {
                        var tempComp = fileValue as Component;
                        if (tempComp != null)
                        {
                            int findID = tempComp.GetInstanceID();
                            string name = components[i].gameObject.name;
                            if (scriptID == findID && goInstanceID != instanceID && !elementName.Equals(name))
                            {
                                Selection.activeTransform = components[i].transform;
                                EditorGUIUtility.PingObject(components[i].gameObject);
                                break;
                            }
                        }
                    }
                }
            }
        }
        
        #endregion

    }
}

