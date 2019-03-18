using System;
using System.Collections.Generic;
using System.IO;

namespace ArtCom.Logging
{
    public abstract class CustomLogInfo
    {
        public virtual string Name
        {
            get { return GetType().Name; }
        }
        public virtual string Prefix
        {
            get
            {
                string baseName = Name;
                string referenceMaxLenPrefix = Logs.Default.Prefix;
                int prefixLen = referenceMaxLenPrefix.Length;

                if (baseName.Length > prefixLen) {
                    baseName = baseName.Substring(0, prefixLen);
                }
                string prefix = baseName;

                return prefix;
            }
        }

        public virtual void InitLog(Log log) {}
    }

}