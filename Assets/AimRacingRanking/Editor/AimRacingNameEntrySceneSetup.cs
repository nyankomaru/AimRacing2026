/*━━━━━━━━━
@file AimRacingNameEntrySceneSetup.cs
@brief 物理キーボード入力用NameEntryシーンを新GameFlowへ接続
@author 24CU0312 久場洸太
@date 2026/09/01
最終更新日 2026/09/07
@remarks MenuからNameEntryを経由してIntroへ進む新GameFlow用シーンを生成する
━━━━━━━━━*/

#if UNITY_EDITOR
using System.Collections.Generic;
using AimRacing.Ranking;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class AimRacingNameEntrySceneSetup
{
    private const string k_NameEntryScenePath = "Assets/!Scenes/NameEntry.unity";
    private const string k_FontAssetPath = "Assets/TextMesh Pro/Fonts/NotoSansJP-Regular SDF.asset";
    private const float k_KeyWidth = 100.0f;
    private const float k_KeyHeight = 82.0f;
    private const float k_KeySpacing = 14.0f;
    private static readonly Vector2 k_CanvasSize = new Vector2(5760.0f, 1080.0f);
    private static readonly Color k_BackgroundColor = new Color(0.035f, 0.075f, 0.14f, 1.0f);
    private static readonly Color k_PanelColor = new Color(0.07f, 0.17f, 0.27f, 0.98f);
    private static readonly Color k_KeyColor = new Color(0.12f, 0.38f, 0.58f, 1.0f);
    private static readonly Color k_FunctionKeyColor = new Color(0.13f, 0.52f, 0.42f, 1.0f);

    private static readonly string[][] k_KeyRows =
    {
        new[] { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" },
        new[] { "A", "S", "D", "F", "G", "H", "J", "K", "L" },
        new[] { "Z", "X", "C", "V", "B", "N", "M" }
    };

    /// <summary>
    /// NameEntryシーンだけを物理キーボード入力用に再生成します。
    /// MenuとBuild Settingsは接続に必要な範囲だけ更新し、Resultとランキングには触れません。
    /// </summary>
    [MenuItem("AimRacing/名前入力シーンを作成・更新")]
    public static void CreateOrUpdate()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(k_FontAssetPath);
        if (font == null)
        {
            Debug.LogError("日本語フォントが見つかりません: " + k_FontAssetPath);
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera();
        Canvas canvas = CreateCanvas();
        // 3画面ゲームの中央画面内へUIをまとめ、左右画面へ入力欄が分散しないようにします。
        RectTransform root = CreatePanel(
            "NameEntryRoot",
            canvas.transform,
            new Vector2(1920.0f, 1080.0f),
            Vector2.zero,
            k_BackgroundColor);

        CreateText(
            "TitleText",
            root,
            "なまえを入力してください",
            new Vector2(1500.0f, 70.0f),
            new Vector2(0.0f, 475.0f),
            44.0f,
            font);

        RectTransform inputPanel = CreatePanel(
            "InputPanel",
            root,
            new Vector2(1250.0f, 130.0f),
            new Vector2(0.0f, 365.0f),
            new Color(0.015f, 0.025f, 0.045f, 1.0f));
        TMP_Text inputText = CreateText(
            "InputText",
            inputPanel,
            "ここに名前が表示されます",
            new Vector2(1180.0f, 105.0f),
            Vector2.zero,
            62.0f,
            font);

        TMP_Text modeText = CreateText(
            "InputModeText",
            root,
            "全角：ひらがな",
            new Vector2(720.0f, 60.0f),
            new Vector2(-300.0f, 265.0f),
            34.0f,
            font);
        TMP_Text compositionText = CreateText(
            "CompositionText",
            root,
            string.Empty,
            new Vector2(720.0f, 60.0f),
            new Vector2(390.0f, 265.0f),
            30.0f,
            font);
        TMP_Text validationMessageText = CreateText(
            "ValidationMessageText",
            root,
            string.Empty,
            new Vector2(1400.0f, 65.0f),
            new Vector2(0.0f, 205.0f),
            34.0f,
            font);
        validationMessageText.color = new Color(1.0f, 0.32f, 0.25f, 1.0f);

        RectTransform keyboardPanel = CreatePanel(
            "KeyboardPanel",
            root,
            new Vector2(1450.0f, 515.0f),
            new Vector2(0.0f, -50.0f),
            k_PanelColor);
        CreateKeyboard(keyboardPanel, font);

        CreateText(
            "GuideText",
            root,
            "半角／全角：入力切替　　Backspace：けす　　Enter：おわり　　Space：使用しません",
            new Vector2(1700.0f, 55.0f),
            new Vector2(0.0f, -485.0f),
            25.0f,
            font);

        // 名前確定前に誤入力を見直せるよう、通常入力UIの手前へ確認パネルを重ねます。
        RectTransform confirmationPanel = CreatePanel(
            "ConfirmationPanel",
            root,
            new Vector2(1250.0f, 430.0f),
            Vector2.zero,
            new Color(0.025f, 0.08f, 0.14f, 1.0f));
        CreateText(
            "ConfirmationQuestionText",
            confirmationPanel,
            "これでいいですか？",
            new Vector2(1100.0f, 80.0f),
            new Vector2(0.0f, 125.0f),
            48.0f,
            font);
        TMP_Text confirmationNameText = CreateText(
            "ConfirmationNameText",
            confirmationPanel,
            string.Empty,
            new Vector2(1100.0f, 110.0f),
            new Vector2(0.0f, 25.0f),
            64.0f,
            font);
        CreateText(
            "ConfirmationGuideText",
            confirmationPanel,
            "Enter：はい　　Backspace／Escape：入力にもどる",
            new Vector2(1100.0f, 65.0f),
            new Vector2(0.0f, -125.0f),
            30.0f,
            font);
        confirmationPanel.gameObject.SetActive(false);

        // 入力中はGameManagerへ依存せず、このシーン内のControllerだけで完結させます。
        GameObject systemObject = new GameObject(
            "NameEntrySystem",
            typeof(AimRacingNameEntryController),
            typeof(NameEntryState));
        AimRacingNameEntryController controller =
            systemObject.GetComponent<AimRacingNameEntryController>();

        ConfigureController(
            controller,
            inputText,
            modeText,
            compositionText,
            confirmationPanel.gameObject,
            confirmationNameText,
            validationMessageText);

        EditorSceneManager.SaveScene(scene, k_NameEntryScenePath);
        UpdateBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("物理キーボード入力用NameEntryを作成し、新GameFlowへ接続しました。");
    }

    private static void CreateKeyboard(RectTransform _parent, TMP_FontAsset _font)
    {
        float[] rowOffsets = { 0.0f, 55.0f, 165.0f };
        for (int i = 0; i < k_KeyRows.Length; ++i)
        {
            string[] row = k_KeyRows[i];
            float rowWidth = row.Length * k_KeyWidth + (row.Length - 1) * k_KeySpacing;
            float startX = -rowWidth * 0.5f + k_KeyWidth * 0.5f + rowOffsets[i];
            float y = 155.0f - i * 105.0f;
            for (int j = 0; j < row.Length; ++j)
            {
                float x = startX + j * (k_KeyWidth + k_KeySpacing);
                CreateKey("Key_" + row[j], _parent, row[j], new Vector2(k_KeyWidth, k_KeyHeight), new Vector2(x, y), k_KeyColor, _font);
            }
        }

        // 機能キーも個別Objectにし、デザイナーが通常キーと別の色・大きさへ調整できます。
        CreateKey("Key_Mode", _parent, "半角／全角", new Vector2(250.0f, k_KeyHeight), new Vector2(-480.0f, -180.0f), k_FunctionKeyColor, _font);
        CreateKey("Key_Backspace", _parent, "Backspace\nけす", new Vector2(250.0f, k_KeyHeight), new Vector2(480.0f, -180.0f), k_FunctionKeyColor, _font);
        CreateKey("Key_Space", _parent, "Space（入力なし）", new Vector2(470.0f, k_KeyHeight), new Vector2(-50.0f, -180.0f), k_KeyColor, _font);
        CreateKey("Key_Enter", _parent, "Enter\nおわり", new Vector2(250.0f, k_KeyHeight), new Vector2(0.0f, -285.0f), k_FunctionKeyColor, _font);
    }

    private static void CreateKey(
        string _name,
        Transform _parent,
        string _label,
        Vector2 _size,
        Vector2 _position,
        Color _color,
        TMP_FontAsset _font)
    {
        RectTransform key = CreatePanel(_name, _parent, _size, _position, _color);
        CreateText("Text", key, _label, _size - new Vector2(8.0f, 8.0f), Vector2.zero, 31.0f, _font);
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("NameEntryCamera", typeof(Camera));
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = k_BackgroundColor;
        camera.orthographic = true;
        cameraObject.transform.position = new Vector3(0.0f, 0.0f, -10.0f);
    }

    private static Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject(
            "NameEntryCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = k_CanvasSize;
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static RectTransform CreatePanel(
        string _name,
        Transform _parent,
        Vector2 _size,
        Vector2 _position,
        Color _color)
    {
        GameObject panelObject = new GameObject(_name, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(_parent, false);
        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.sizeDelta = _size;
        rect.anchoredPosition = _position;
        panelObject.GetComponent<Image>().color = _color;
        return rect;
    }

    private static TMP_Text CreateText(
        string _name,
        Transform _parent,
        string _text,
        Vector2 _size,
        Vector2 _position,
        float _fontSize,
        TMP_FontAsset _font)
    {
        GameObject textObject = new GameObject(_name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(_parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.sizeDelta = _size;
        rect.anchoredPosition = _position;
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = _text;
        text.font = _font;
        text.fontSize = _fontSize;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = false;
        return text;
    }

    private static void ConfigureController(
        AimRacingNameEntryController _controller,
        TMP_Text _inputText,
        TMP_Text _modeText,
        TMP_Text _compositionText,
        GameObject _confirmationPanel,
        TMP_Text _confirmationNameText,
        TMP_Text _validationMessageText)
    {
        SerializedObject serialized = new SerializedObject(_controller);
        serialized.FindProperty("m_inputText").objectReferenceValue = _inputText;
        serialized.FindProperty("m_modeText").objectReferenceValue = _modeText;
        serialized.FindProperty("m_compositionText").objectReferenceValue = _compositionText;
        serialized.FindProperty("m_confirmationPanel").objectReferenceValue = _confirmationPanel;
        serialized.FindProperty("m_confirmationNameText").objectReferenceValue = _confirmationNameText;
        serialized.FindProperty("m_validationMessageText").objectReferenceValue = _validationMessageText;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void UpdateBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes =
            new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(_scene => _scene.path == k_NameEntryScenePath)) { return; }

        int mapIndex = scenes.FindIndex(_scene => _scene.path.EndsWith("/Map.unity"));
        int insertIndex = mapIndex < 0 ? scenes.Count : mapIndex;
        scenes.Insert(insertIndex, new EditorBuildSettingsScene(k_NameEntryScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

}
#endif
