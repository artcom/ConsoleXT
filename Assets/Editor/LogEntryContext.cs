using System;
using System.Collections.Generic;
using System.Text;

using UnityEngine;
using UnityEditor;

namespace ArtCom.Logging.Editor {

    [Serializable]
    public struct LogEntryContext
    {
        [SerializeField] private string _assetPath;
        [SerializeField] private string _objPath;

        private object _obj;

        public object RawObject
        {
            get
            {
                if (_obj == null || (_obj as UnityEngine.Object) == null)
                    _obj = RetrieveObject();
                return _obj;
            }
        }
        public UnityEngine.Object UnityObject
        {
            get { return RawObject as UnityEngine.Object; }
        }

        public LogEntryContext(object obj)
        {
            _obj = obj;
            _assetPath = null;
            _objPath = null;

            if (_obj is UnityEngine.Object)
            {
                _assetPath = AssetDatabase.GetAssetOrScenePath(_obj as UnityEngine.Object);
                _objPath = GetSceneObjectPath(_obj as UnityEngine.Object);
            }
        }

        private object RetrieveObject()
        {
            object obj = null;
            
            if (obj == null && !string.IsNullOrEmpty(_objPath  )) obj = FindSceneObjectByPath(_objPath);
            if (obj == null && !string.IsNullOrEmpty(_assetPath)) obj = AssetDatabase.LoadMainAssetAtPath(_assetPath);

            return obj;
        }

        private static UnityEngine.Object FindSceneObjectByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            // GameObject.Find can actuall resolve object paths like "/abc/def/x"
            return GameObject.Find(path);
        }
        private static string GetSceneObjectPath(UnityEngine.Object obj)
        {
            Component component = obj as Component;
            GameObject gameObj = (component != null) ? component.gameObject : obj as GameObject;
            if (gameObj == null) return null;

            // Determine the object's full name in the scene graph
            StringBuilder fullNameBuilder = new StringBuilder();
            UnityEngine.Transform current = gameObj.transform;
            while (current != null) {
                if (fullNameBuilder.Length > 0)
                    fullNameBuilder.Insert(0, '/');
                fullNameBuilder.Insert(0, current.name);
                current = current.parent;
            }
            return fullNameBuilder.ToString();
        }
    }

}