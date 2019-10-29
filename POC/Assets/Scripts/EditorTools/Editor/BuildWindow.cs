﻿using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class BuildWindow : EditorWindow
{
    Vector2 m_ScrollPos;
    QuickstartData quickstartData = new QuickstartData();

    public enum GameLoopMode
    {
        Server,
        Client,
        Undefined,
    }

    public enum EditorRole
    {
        Unused,
        Client,
        Server,
    }

    [Serializable]
    class QuickstartData
    {
        public EditorRole editorRole;
        public int clientCount = 1;
        public bool headlessServer = true;
        public List<QuickstartEntry> entries = new List<QuickstartEntry>();
    }

    [Serializable]
    class QuickstartEntry
    {
        public GameLoopMode gameLoopMode = GameLoopMode.Client;

        public bool runInEditor;
        public bool headless;
    }

    [MenuItem("Project Managment/Windows/Project Tools")]
    public static void ShowWindow()
    {
        GetWindow<BuildWindow>(false, "Project Tools", true);
    }

    void OnGUI()
    {
        m_ScrollPos = GUILayout.BeginScrollView(m_ScrollPos);

        GUILayout.Label("Project", EditorStyles.boldLabel);

        GUILayout.TextArea(Application.dataPath.BeforeLast("Assets"));

        DrawBuild();

        GUILayout.Space(10.0f);

        DrawQuickStart();

        GUILayout.EndScrollView();
    }

    void DrawBuild()
    {
        // Title
        GUILayout.Label("Build", EditorStyles.boldLabel);

        // Build Times
        GUILayout.Label("Build times:");
        string serverStr = PrettyPrintTimeStamp(TimeLastBuildGame(GameLoopMode.Server));
        string clientStr = PrettyPrintTimeStamp(TimeLastBuildGame(GameLoopMode.Client));

        GUILayout.BeginHorizontal();
        {
            GUILayout.Space(20);
            GUILayout.Label("- Build Time Server", GUILayout.Width(130));
            EditorGUILayout.SelectableLabel(serverStr, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        {
            GUILayout.Space(20);
            GUILayout.Label("- Build Time Client", GUILayout.Width(130));
            EditorGUILayout.SelectableLabel(clientStr, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }
        GUILayout.EndHorizontal();


        // Build All
        GUILayout.Label("Rebuild S.Alone", EditorStyles.boldLabel);
        GUILayout.BeginVertical(EditorStyles.textArea);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Build All", GUILayout.ExpandWidth(true));
        GUILayout.BeginHorizontal(GUILayout.Width(100));
        if (GUILayout.Button("Build"))
        {
            BuildServer();
            BuildClient();
            GUIUtility.ExitGUI();
        }

        GUILayout.EndHorizontal();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Build Server", GUILayout.ExpandWidth(true));
        GUILayout.BeginHorizontal(GUILayout.Width(100));
        if (GUILayout.Button("Build"))
        {
            BuildServer();
            GUIUtility.ExitGUI();
        }

        GUILayout.EndHorizontal();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Build Client", GUILayout.ExpandWidth(true));
        GUILayout.BeginHorizontal(GUILayout.Width(100));
        if (GUILayout.Button("Build"))
        {
            BuildClient();
            GUIUtility.ExitGUI();
        }

        GUILayout.EndHorizontal();
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    void DrawQuickStart()
    {
        GUILayout.BeginVertical();

        GUILayout.Label("Quick Start", EditorStyles.boldLabel);

        var entryCount = quickstartData.clientCount + 1;
        // Make sure we have enough entries
        var minEntryCount = Math.Max(entryCount, 2);
        while (minEntryCount > quickstartData.entries.Count())
            quickstartData.entries.Add(new QuickstartEntry());

        string str = "Starting Game - Server (Headless) & " + quickstartData.clientCount.ToString() + " clients";
        GUILayout.Label(str, EditorStyles.boldLabel);

        Color defaultGUIBackgrounColor = GUI.backgroundColor;
        // Quick start buttons
        GUILayout.BeginHorizontal();
        {
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Start"))
            {
                for (int i = 0; i < entryCount; i++)
                {
                    if (quickstartData.entries[i].runInEditor)
                        continue;
                        //EditorLevelManager.StartGameInEditor();
                    else
                    {
                        RunBuild(quickstartData.entries[i].gameLoopMode);
                    }
                }
            }
            GUI.backgroundColor = defaultGUIBackgrounColor;

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Stop All"))
            {
                StopAll();
            }
            GUI.backgroundColor = defaultGUIBackgrounColor;
        }
        GUILayout.EndHorizontal();

        // Settings
        EditorGUI.BeginChangeCheck();

        quickstartData.clientCount = EditorGUILayout.IntField("Clients", quickstartData.clientCount);

        quickstartData.editorRole = (EditorRole)EditorGUILayout.EnumPopup("Use Editor as", quickstartData.editorRole);

        quickstartData.entries[0].gameLoopMode = GameLoopMode.Server;
        quickstartData.entries[0].headless = quickstartData.headlessServer;

        quickstartData.entries[0].runInEditor = quickstartData.editorRole == EditorRole.Server;
        quickstartData.entries[1].runInEditor = quickstartData.editorRole == EditorRole.Client;

        for (var i = 1; i < entryCount; i++)
        {
            quickstartData.entries[i].gameLoopMode = GameLoopMode.Client;
            quickstartData.entries[i].headless = false;
        }

        // Draw entries
        GUILayout.Label("Started processes:");
        for (var i = 0; i < entryCount; i++)
        {
            var entry = quickstartData.entries[i];
            GUILayout.BeginVertical();
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Space(20);

                    GUILayout.Label("- Stand Alone Build", GUILayout.Width(130));

                    EditorGUILayout.SelectableLabel(entry.gameLoopMode.ToString(), EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        GUILayout.EndVertical();
    }

    public static void BuildServer()
    {
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Server.unity" },
            locationPathName = GetBuildPath() + "/Server/Server.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.EnableHeadlessMode
        };
        BuildPipeline.BuildPlayer(buildPlayerOptions);
        Debug.Log("Server Build successful");
    }

    public static void BuildClient()
    {
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Client.unity" },
            locationPathName = GetBuildPath() + "/Client/Client.exe",
            target = BuildTarget.StandaloneWindows64
        };
        BuildPipeline.BuildPlayer(buildPlayerOptions);
        Debug.Log("Client Build successful");
    }

    static void StopAll()
    {
        KillAllProcesses();
        EditorApplication.isPlaying = false;
    }

    static void KillAllProcesses()
    {
        var buildExe = GetBuildExe(GameLoopMode.Server);

        var processName = Path.GetFileNameWithoutExtension(buildExe);
        var processes = System.Diagnostics.Process.GetProcesses();
        foreach (var process in processes)
        {
            if (process.HasExited)
                continue;

            try
            {
                if (process.ProcessName != null && process.ProcessName == processName)
                {
                    process.Kill();
                }
            }
            catch (InvalidOperationException)
            {

            }
        }

        buildExe = GetBuildExe(GameLoopMode.Client);

        processName = Path.GetFileNameWithoutExtension(buildExe);
        processes = System.Diagnostics.Process.GetProcesses();
        foreach (var process in processes)
        {
            if (process.HasExited)
                continue;

            try
            {
                if (process.ProcessName != null && process.ProcessName == processName)
                {
                    process.Kill();
                }
            }
            catch (InvalidOperationException)
            {

            }
        }
    }

    static string GetBuildPath()
    {
        return "Builds";
    }

    static string GetBuildExe(GameLoopMode mode)
    {
        if (mode == GameLoopMode.Server)
            return "Server/Server.exe";
        else if (mode == GameLoopMode.Client)
            return "Client/Client.exe";
        else
            return "";
    }

    public static void RunBuild(GameLoopMode mode)
    {
        var buildPath = GetBuildPath();
        var buildExe = GetBuildExe(mode);
        Debug.Log("Starting " + buildPath + "/" + buildExe);
        var process = new System.Diagnostics.Process();
        process.StartInfo.FileName = Application.dataPath + "/../" + buildPath + "/" + buildExe;
        process.StartInfo.WorkingDirectory = buildPath;
        process.Start();
    }

    static string PrettyPrintTimeStamp(DateTime time)
    {
        var span = DateTime.Now - time;
        if (span.TotalMinutes < 60)
            return span.Minutes + " mins ago";
        if (DateTime.Now.Date == time.Date)
            return time.ToShortTimeString() + " today";
        if (DateTime.Now.Date.AddDays(-1) == time.Date)
            return time.ToShortTimeString() + " yesterday";
        return "" + time;
    }

    static DateTime TimeLastBuildGame(GameLoopMode mode)
    {
        string buildPath = GetBuildPath();
        if (mode == GameLoopMode.Server)
            buildPath += "/Server";
        else if (mode == GameLoopMode.Client)
            buildPath += "/Client";

        return Directory.GetLastWriteTime(buildPath);
    }
}

