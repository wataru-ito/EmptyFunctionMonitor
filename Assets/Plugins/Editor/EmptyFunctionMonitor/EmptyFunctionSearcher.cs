using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

namespace EmptyFunctionMonitor
{
	/// <summary>
	/// 空関数検索機
	/// </summary>
	internal class EmptyFunctionSearcher : EditorWindow
	{
		string _directoryPath;
		bool _awakeFlg;
		bool _startFlg = true;
		bool _updateFlg = true;
		bool _lateupdateFlg;

		List<EmptyFunctionInfo> _result;
		System.Action<List<EmptyFunctionInfo>> _callback;


		//------------------------------------------------------
		// static function
		//------------------------------------------------------

		internal static EmptyFunctionSearcher Open(Rect position, System.Action<List<EmptyFunctionInfo>> callback)
		{
			var win = CreateInstance<EmptyFunctionSearcher>();
			win._callback = callback;
			win.ShowUtility();

			var p = win.position;
			p.center = position.center;
			win.position = p;

			return win;
		}


		//------------------------------------------------------
		// unity system function
		//------------------------------------------------------

		void OnEnable()
		{
			titleContent = new GUIContent("空関数検索");

			_directoryPath = Application.dataPath;
		}

		void OnLostFocus()
		{
			// DisplayCancelableProgressBar()でもフォーカスが失われるっぽい
			if (_result != null)
				return;

			Close();
		}

		void OnGUI()
		{
			EditorGUIUtility.labelWidth = 100f;

			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.LabelField("検索フォルダ", _directoryPath.Substring(Application.dataPath.Length - 6));
				if (GUILayout.Button("変更", GUILayout.Width(32)))
				{
					var path = EditorUtility.OpenFolderPanel("検索フォルダ", _directoryPath, string.Empty);
					if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
					{
						_directoryPath = path;
					}
				}
			}

			_awakeFlg = EditorGUILayout.Toggle("Awake", _awakeFlg);
			_startFlg = EditorGUILayout.Toggle("Start", _startFlg);
			_updateFlg = EditorGUILayout.Toggle("Update", _updateFlg);
			_lateupdateFlg = EditorGUILayout.Toggle("LateUpdate", _lateupdateFlg);

			EditorGUILayout.Space();

			GUI.enabled = Directory.Exists(_directoryPath) && (_awakeFlg || _startFlg || _updateFlg || _lateupdateFlg);
			if (GUILayout.Button("実行"))
			{
				Search();
			}
			GUI.enabled = true;
		}


		//------------------------------------------------------
		// regex
		//------------------------------------------------------

		Regex CreateRegex()
		{
			var pattern = new StringBuilder();
			if (_awakeFlg) AppendFunction(pattern, "Awake");
			if (_startFlg) AppendFunction(pattern, "Start");
			if (_updateFlg) AppendFunction(pattern, "Update");
			if (_lateupdateFlg) AppendFunction(pattern, "LateUpdate");

			pattern.Insert(0, @"[ \t]*void\s+(");
			pattern.Append(@")\s*\(\s*\)\s*\{\s*\}");
			return new Regex(pattern.ToString());
		}
		
		static void AppendFunction(StringBuilder sb, string functionName)
		{
			if (sb.Length > 0) sb.Append("|");
			sb.Append(functionName);
		}


		//------------------------------------------------------
		// search
		//------------------------------------------------------

		void Search()
		{
			_result = new List<EmptyFunctionInfo>();

			var regex = CreateRegex();

			try
			{
				var stopwatch = System.Diagnostics.Stopwatch.StartNew();

				var scripts = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
				for (int i = 0; i < scripts.Length; ++i)
				{
					if (EditorUtility.DisplayCancelableProgressBar("空関数検索中",
						string.Format("{0}/{1} {2}", i, scripts.Length, scripts[i]),
						i / (float)scripts.Length))
					{
						break;
					}

					Search(scripts[i], regex);
				}

				stopwatch.Stop();
				Debug.LogFormat("EmptyFunctionSearch : {0}ms", stopwatch.ElapsedMilliseconds);
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}

			if (_callback != null)
			{
				_callback(_result);
			}

			Close();
		}

		void Search(string filePath, Regex regex)
		{
			var code = File.ReadAllText(filePath);

			for (var match = regex.Match(code); match.Success; match = match.NextMatch())
			{
				// 直前にvirtualが付いてたら無視
				// > 正規表現でvirtualなしがうまく作れなかったのでこうする…
				var index = match.Index;
				if (index > 7 &&
					code[index - 7] == 'v' &&
					code[index - 6] == 'i' &&
					code[index - 5] == 'r' &&
					code[index - 4] == 't' &&
					code[index - 3] == 'u' &&
					code[index - 2] == 'a' &&
					code[index - 1] == 'l')
				{
					continue;
				}

				var info = new EmptyFunctionInfo(
					filePath.Substring(Application.dataPath.Length - 6),
					GetLineCount(ref code, index),
					GetFuncName(match.Value));

				_result.Add(info);
			}
		}

		static string GetFuncName(string matchValue)
		{
			var m = Regex.Match(matchValue, @"void\s+\w+\s*\(");
			return m.Success ?
				m.Value.Substring(4, m.Value.Length - 5).Trim() :
				matchValue;
		}

		static int GetLineCount(ref string code, int index)
		{
			int lineCount = 1;
			// 改行コードが\rだけってほぼないでしょ！（
			for (int i = 0; i < index; ++i)
			{
				if (code[i] == '\n')
					++lineCount;
			}
			return lineCount;
		}
	}
}