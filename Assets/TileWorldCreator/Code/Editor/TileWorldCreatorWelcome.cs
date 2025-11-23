using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Linq;

namespace TWC
{
    public class TileWorldCreatorWelcome : EditorWindow
    {
        List<string> choices = new List<string>() { "On startup", "Never" };

        int selectedDropdownIndex;

        public string documentationLink = "https://doorfortyfour.github.io/TileWorldCreator";
        public string gettingStartedLink = "https://doorfortyfour.github.io/TileWorldCreator/#/GettingStarted?id=quick-start";
        public string faqLink = "https://doorfortyfour.github.io/TileWorldCreator/#/faq";
        public string videoGettingStartedLink = "https://www.youtube.com/watch?v=cscc5_BeY58";
        public string websiteLink = "https://www.tileworldcreator.com/";
        public string assetStoreLink = "https://u3d.as/2Dz4";
        public string emailLink = "mailto:hello@giantgrey.com";

        [InitializeOnLoadMethod]
        public static void Init() 
        {
            // Very first time after installation and compilation
            var _keyValueFirstTime = EditorPrefs.GetBool("TileWorldCreatorWelcomeFirstTime");

            if (!_keyValueFirstTime)
            {
                EditorPrefs.SetBool("TileWorldCreatorWelcomeFirstTime", true);
                ShowWindow();
            }  
        }

        static TileWorldCreatorWelcome()
        {
            EditorApplication.delayCall += ShowOnStartup;
        }

        private static void ShowOnStartup()
        {
            var _keyValue = EditorPrefs.GetString("TileWorldCreatorWelcome");
            var _keyValueFirstTime = SessionState.GetBool("TileWorldCreatorWelcomeFirstTime", false);

            if (!_keyValueFirstTime && _keyValue == "startup") 
            {
                SessionState.SetBool("TileWorldCreatorWelcomeFirstTime", true);
                ShowWindow();
            }
        }

        [MenuItem("Tools/TileWorldCreator/Welcome", false, 200)]
            static void ShowWindow()
            {
                EditorWindow wnd = EditorWindow.CreateWindow<TileWorldCreatorWelcome>();
                wnd.titleContent = new GUIContent("TileWorldCreator - Welcome");
                wnd.maxSize = new Vector2(500f, 820f);
                wnd.minSize = wnd.maxSize;
                wnd.Show();
            }


            public void CreateGUI()
            {
                var _root = rootVisualElement;
                WindowGUI(_root);
            }

            public VisualElement WindowGUI(VisualElement _root)
            {
                
                // _root.style.backgroundColor = Color.black;

                var _main = new VisualElement();
                _main.style.flexGrow = 1;
                _main.style.alignItems = Align.Stretch;
                

                var _header = new VisualElement();
                _header.style.backgroundColor = Color.black;
                _header.style.alignContent = Align.Center;
                _header.style.alignItems = Align.Center;

                var _logo = new VisualElement();
                _logo.style.backgroundImage = (Texture2D)(AssetDatabase.LoadAssetAtPath(GetRelativeResPath() + "/twcLogo.png", typeof(Texture2D)));
                _logo.style.width = 170;
                _logo.style.height = 75;
                _logo.style.marginTop = 50;

                var _headerLabel = new Label();
                _headerLabel.text = "Thank you for purchasing TileWorldCreator\n" + "and supporting a small indie game studio! ❤\n" + 
                "\n<size=12>If you enjoy using TileWorldCreator, please consider leaving a review. \nYour feedback greatly supports the future development of the asset!</size>";
                _headerLabel.style.fontSize = 16;
                _headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                _headerLabel.style.marginLeft = 10;
                _headerLabel.style.marginRight = 10;
                _headerLabel.style.marginTop = 10;
                _headerLabel.style.marginBottom = 40;
                _headerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

                _header.Add(_logo);
                _header.Add(_headerLabel);

                var _label = new Label();
                _label.style.fontSize = 12;
                _label.style.marginBottom = 20;
                _label.style.marginLeft = 20;
                _label.style.marginRight = 20;
                _label.style.marginTop = 20; 
                _label.style.whiteSpace = WhiteSpace.Normal;
                
                
                _label.text = "<b>Getting started</b> \n" +
                "To get started quickly head over to the getting started guide or the getting started video.\n\n" +
                "<b>Demo scenes</b> \n" +
                "You can find all demo scenes in the folder: \n<i>TileWorldCreator / Demo </i> \n\n" +
                "If you're encountering pink materials make sure to convert them to your current render pipeline (URP or HDRP) using the Render Pipeline Converter (Window / Rendering / Render Pipeline Converter)";

                // _main.Add(_logo);
                _main.Add(_header);
                _main.Add(_label);

                var _gettingStarted = new Button();
                _gettingStarted.text = "Getting Started";
                _gettingStarted.style.marginLeft = 20;
                _gettingStarted.style.marginRight = 20;
                _gettingStarted.style.height = 25;
                _gettingStarted.RegisterCallback<ClickEvent>(evt => 
                {
                    Application.OpenURL(gettingStartedLink);
                });

                var _gettingStartedVideo = new Button();
                _gettingStartedVideo.text = "Getting Started Video";
                _gettingStartedVideo.style.marginLeft = 20;
                _gettingStartedVideo.style.marginRight = 20;
                _gettingStartedVideo.style.height = 25;
                _gettingStartedVideo.RegisterCallback<ClickEvent>(evt =>
                {
                    Application.OpenURL(videoGettingStartedLink);
                });

                var _documentation = new Button();
                _documentation.text = "Documentation";
                _documentation.style.marginLeft = 20;
                _documentation.style.marginRight = 20;
                _documentation.style.height = 25;
                _documentation.RegisterCallback<ClickEvent>(evt =>
                {
                    Application.OpenURL(documentationLink);
                });

                var _faq = new Button();
                _faq.text = "FAQ";
                _faq.style.marginLeft = 20;
                _faq.style.marginRight = 20;
                _faq.style.height = 25;
                _faq.RegisterCallback<ClickEvent>(evt =>
                {
                    Application.OpenURL(faqLink);
                });

                var _space1 = new VisualElement();
                // _space1.style.backgroundColor = Color.black;
                _space1.style.maxHeight = 10;
                _space1.style.flexDirection = FlexDirection.Row;
                _space1.style.flexGrow = 1;

                var _website = new Button();
                _website.text = "Website";
                _website.style.marginLeft = 20;
                _website.style.marginRight = 20;
                _website.style.height = 25;
                _website.RegisterCallback<ClickEvent>(evt =>
                {
                    Application.OpenURL(websiteLink);
                });

                var _assetStore = new Button();
                _assetStore.text = "Asset Store";
                _assetStore.style.marginLeft = 20;
                _assetStore.style.marginRight = 20;
                _assetStore.style.height = 25;
                _assetStore.RegisterCallback<ClickEvent>(evt =>
                {
                    Application.OpenURL(assetStoreLink);
                });

                var _support = new Button();
                _support.text = "Support";
                _support.style.marginLeft = 20;
                _support.style.marginRight = 20;
                _support.style.height = 25;
                _support.RegisterCallback<ClickEvent>(evt => 
                {
                    Application.OpenURL(emailLink);
                });

                var _space2 = new VisualElement();
                // _space2.style.backgroundColor = Color.black;
                _space2.style.maxHeight = 10;
                _space2.style.flexDirection = FlexDirection.Row;
                _space2.style.flexGrow = 1;

                var _moreText = new Label();
                _moreText.text = "More by <b>Giant Grey</b>";
                _moreText.style.fontSize = 12;
                _moreText.style.marginBottom = 5;
                _moreText.style.marginLeft = 20;
                _moreText.style.marginRight = 20;
                _moreText.style.whiteSpace = WhiteSpace.Normal;

                var _databrain = new Button();
                _databrain.text = "Databrain - Ultimate Data management tool";
                _databrain.style.marginLeft = 20;
                _databrain.style.marginRight = 20;
                _databrain.style.height = 25;
                _databrain.RegisterCallback<ClickEvent>(evt => 
                {
                    Application.OpenURL("https://assetstore.unity.com/packages/tools/game-toolkits/databrain-ultimate-scriptable-objects-framework-244557");
                });

                var _pathgrid = new Button();
                _pathgrid.text = "Pathgrid";
                _pathgrid.style.marginLeft = 20;
                _pathgrid.style.marginRight = 20;
                _pathgrid.style.height = 25;
                _pathgrid.RegisterCallback<ClickEvent>(evt => 
                {
                    Application.OpenURL("https://assetstore.unity.com/packages/tools/level-design/pathgrid-277374");
                });

                var _marz = new Button();
                _marz.text = "MarZ: Tactical Base Defense";
                _marz.style.marginLeft = 20;
                _marz.style.marginRight = 20;
                _marz.style.height = 25;
                _marz.RegisterCallback<ClickEvent>(evt => 
                {
                    Application.OpenURL("https://store.steampowered.com/app/682530/MarZ_Tactical_Base_Defense/");
                });

                _main.Add(_gettingStarted);
                _main.Add(_gettingStartedVideo);
                _main.Add(_documentation);
                _main.Add(_faq);
                _main.Add(_space1);
                _main.Add(_website);
                _main.Add(_assetStore);
                _main.Add(_website);
                _main.Add(_assetStore);
                _main.Add(_support);
                _main.Add(_space2);
                _main.Add(_moreText);
                _main.Add(_databrain);
                _main.Add(_pathgrid);
                _main.Add(_marz);


                var _footer = new VisualElement(); 
                _footer.style.flexDirection = FlexDirection.Row;
                _footer.style.maxHeight = 20;
                _footer.style.height = 20;
                _footer.style.flexGrow = 1;
                _footer.style.backgroundColor = new Color(50f/255f, 50f/255f, 50f/255f);
                

                var _dropdown = new DropdownField(choices, selectedDropdownIndex);
                _dropdown.label = "Show window:";
                _dropdown.RegisterValueChangedCallback(x =>
                {
                    if (x.newValue != x.previousValue)
                    {
                        switch (_dropdown.index)
                        {
                            case 0:
                                EditorPrefs.SetString("TileWorldCreatorWelcome", "startup");
                                break;
                            case 1:
                                EditorPrefs.SetString("TileWorldCreatorWelcome", "never");
                                break;
                        }
                    }
                });

                var _selectedDropdown = EditorPrefs.GetString("TileWorldCreatorWelcome");
                switch (_selectedDropdown)
                {
                    case "startup":
                        _dropdown.index = 0;
                        break;
                    case "never":
                        _dropdown.index = 1;
                        break;
                }

                _footer.Add(_dropdown);
                _root.Add(_main);
                _root.Add(_footer);

                return _root;
            }

            public static string GetRelativeResPath()
            {
            
                var _path = EditorPrefs.GetString("TWC_RESPATH");
                if (!AssetDatabase.IsValidFolder(_path))
                {
                    _path = "";
                }

                if (string.IsNullOrEmpty(_path))
                {
                    var _res = System.IO.Directory.EnumerateFiles("Assets/", "TWCResPath.cs", System.IO.SearchOption.AllDirectories);

                    var _found = _res.FirstOrDefault();
                    if (!string.IsNullOrEmpty(_found))
                    {
                        _path = _found.Replace("TWCResPath.cs", "").Replace("\\", "/");
                        //_path = Path.Combine(_path, _theme);

                        // Cache resource path
                        EditorPrefs.SetString("TWC_RESPATH", _path);
                    }
                }

                return _path;
            }
    }
}