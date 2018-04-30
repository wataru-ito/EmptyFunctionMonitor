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
		/// <summary>
		/// Matchからヒットした関数名情報を取り出すより[FuncName:Regex]のペアで管理した方が楽なので…
		/// > でも速度的にはRegex一つにした方が早いんだろうなぁ…
		/// > あれば便利かなってfuncName表示してるけどそもそもいる？
		/// </summary>
		struct RegexInfo
		{
			public string funcName;
			public Regex regex;
		}


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
			_awakeFlg = EditorGUILayout.Toggle("Awake", _awakeFlg);
			_startFlg = EditorGUILayout.Toggle("Start", _startFlg);
			_updateFlg = EditorGUILayout.Toggle("Update", _updateFlg);
			_lateupdateFlg = EditorGUILayout.Toggle("LateUpdate", _lateupdateFlg);

			EditorGUILayout.Space();

			GUI.enabled = _awakeFlg || _startFlg || _updateFlg || _lateupdateFlg;
			if (GUILayout.Button("実行"))
			{
				Search();
			}
			GUI.enabled = true;
		}


		//------------------------------------------------------
		// regex
		//------------------------------------------------------

		List<RegexInfo> CreateRegex()
		{
			var regexList = new List<RegexInfo>();
			if (_awakeFlg) regexList.Add(CreateRegex("Awake"));
			if (_startFlg) regexList.Add(CreateRegex("Start"));
			if (_updateFlg) regexList.Add(CreateRegex("Update"));
			if (_lateupdateFlg) regexList.Add(CreateRegex("LateUpdate"));
			return regexList;
		}

		RegexInfo CreateRegex(string funcName)
		{
			var info = new RegexInfo();
			info.funcName = funcName;
			// \{\s*\} をformatの中に入れちゃうと警告が出るので分けてる
			info.regex = new Regex(string.Format(@"[ \t]*void\s+{0}\s*\(\s*\)\s*", funcName) + @"\{\s*\}");
			return info;
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

			var regexList = CreateRegex();

			try
			{
				var scripts = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
				for (int i = 0; i < scripts.Length; ++i)
				{
					if (EditorUtility.DisplayCancelableProgressBar("空関数検索中",
						string.Format("{0}/{1}\n\n{2}", i, scripts.Length, scripts[i]),
						i / (float)scripts.Length))
					{
						break;
					}

					Search(scripts[i], regexList);
				}
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

		void Search(string filePath, List<RegexInfo> regexList)
		{
			var code = File.ReadAllText(filePath);

			foreach (var regexInfo in regexList)
			{
				var match = regexInfo.regex.Match(code);
				if (match.Success)
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
						regexInfo.funcName);

					_result.Add(info);
				}
			}
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