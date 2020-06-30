
using System;

using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;
using System.Text;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Unity.EditorCoroutines.Editor;
namespace edu.ucf.caam
{
    [Serializable]
    public struct LogMessage
    {
        public string msg;
        public string st;
    }
    // ensure class initializer is called whenever scripts recompile
    [InitializeOnLoadAttribute]
    public static class CamBridge
    {

        public static string output = "";
        public static string stack = "";
        public static string secret = "";
        public struct LogMessages
        {
            public string ts;
            public List<LogMessage> log;
        }
        public static LogMessages logMessages = new LogMessages { log = new List<LogMessage>() };
        public static int logCount = 0;
        static LogMessage lm;
        /// <summary>
        /// This function is called when this object becomes enabled and active
        /// </summary>
        static CamBridge()
        {
            string[] s = Application.dataPath.Split('/');
            string projectName = s[s.Length - 2];
            if (!EditorPrefs.HasKey("secret-" + projectName))
            {
                UnityEditor.EditorApplication.playModeStateChanged += DidPlay;
                var secretPath = Application.dataPath.Substring(0, Application.dataPath.Length - 7) + "/.ucf/.secret";
                if (File.Exists(secretPath))
                {
                    StreamReader sr = new StreamReader(secretPath, false);
                    secret = sr.ReadLine();
                    sr.Close();
                    EditorPrefs.SetString("secret-" + projectName, secret);
                }
            }
            else
            {
                secret = EditorPrefs.GetString("secret-" + projectName);
            }
            Application.logMessageReceived += HandleLog;
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void DidReloadScripts()
        {
            // Debug.Log("Reloaded Scripts");
            EditorCoroutineUtility.StartCoroutineOwnerless(Post("https://plato.mrl.ai:8081/git/event", "{\"compiled\":true}"));
            File.Delete(Application.dataPath.Substring(0, Application.dataPath.Length - 7) + "/.ucf/.compilerError");
        }
        private static String prettyPrintErrors()
        {
            string str = "";
            foreach (var msg in logMessages.log)
            {
                str += msg.msg + "\n\r";
            }
            return str;
        }

        private static void DidPlay(PlayModeStateChange state)
        {
            // Debug.Log("Did Play");
        }

        static IEnumerator Post(string url, string bodyJsonString)
        {
            var request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJsonString);
            request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
            // request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            // Debug.Log("secret: " + secret);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("secret", secret);
            yield return request.SendWebRequest();
        }
        // keep a copy of the executing script
        private static EditorCoroutine coroutine;

        static IEnumerator EditorAttempt(float waitTime)
        {
            yield return new EditorWaitForSeconds(waitTime);
            if (logMessages.log.Count > 0)
            {
                EditorCoroutineUtility.StartCoroutineOwnerless(Post("https://plato.mrl.ai:8081/git/event", JsonUtility.ToJson(logMessages)));
                File.WriteAllText(Application.dataPath.Substring(0, Application.dataPath.Length - 7) + "/.ucf/.compilerError", prettyPrintErrors());
                logMessages.log.Clear();
            }
        }

        static void HandleLog(string logString, string stackTrace, LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                    // Debug.Log("Error Message");
                    break;
                default:
                    return;
            }
            output = logString;
            stack = stackTrace;
            lm = new LogMessage
            {
                st = stack,
                msg = output,
            };
            logMessages.log.Add(lm);
            if (logCount == 0)
            {
                logMessages.ts = DateTime.Now.ToString();
                coroutine = EditorCoroutineUtility.StartCoroutineOwnerless(EditorAttempt(0.5f));
            }
            else
            {
                EditorCoroutineUtility.StopCoroutine(coroutine);
                coroutine = EditorCoroutineUtility.StartCoroutineOwnerless(EditorAttempt(0.5f));
            }
            // sw.Close();
        }
    }
}