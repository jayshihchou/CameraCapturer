using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How To Use:
/// 1. Attach this to Camera, that you want to capture.
/// 2. Set capture size, position (or full screen), duration, FPS and output path.
/// 3. GenerateBuffer().
/// 4. StartCapture().
/// 5. Set output format. And OutputResults().
/// 
/// Example can also found in Scenes folder.
/// </summary>
public class CameraCapturer : MonoBehaviour
{
	#region ========================== Parameters ==========================
	[Header("Mouse middle button can unfull screen.")]
	[SerializeField] bool fullScreen;
	public bool FullScreen
	{
		get { return fullScreen; }
		set
		{
			if (fullScreen != value)
			{
				fullScreen = value;
				Reset("full screen mode has changed.");
			}
		}
	}
	Rect currentScreen = new Rect();
	[SerializeField] Rect position = new Rect(0f, 0f, 500f, 300f);
	/// <summary>
	/// Return current capture rect, if is in fullScreen mode will return fullScreen rect. Note: Setting this will also set fullScreen to false.
	/// </summary>
	public Rect CaptureRect
	{
		get { return fullScreen ? currentScreen : position; }
		set
		{
			fullScreen = false;
			position = value;
			Reset("capture size has changed.");
		}
	}
	/// <summary>
	/// Current capture rect size, if is in fullScreen mode will return fullScreen size. Note: Setting this will also set fullScreen to false.
	/// </summary>
	public Vector2 CaptureSize
	{
		get { return fullScreen ? currentScreen.size : position.size; }
		set
		{
			fullScreen = false;
			position.size = value;
			Reset("capture size has changed.");
		}
	}
	[SerializeField] float recordDuration = 3f;
	/// <summary>
	/// Duration for recoding.
	/// </summary>
	public float RecordDuration
	{
		get { return recordDuration; }
		set
		{
			recordDuration = value;
			Reset("record duration has changed.");
		}
	}
	[SerializeField] int captureFPS = 30;
	/// <summary>
	/// Capture FPS.
	/// </summary>
	public int CaptureFPS
	{
		get { return captureFPS; }
		set
		{
			captureFPS = Mathf.Clamp(value, 1, value);
			Reset("FPS has changed.");
		}
	}

	[Range(1f, 10f)]
	public float CountdownTime = 3f;

	// privates
	bool canCapture = false;
	bool capturing = false;
	bool result = false;
	RenderTexture[] buffers = null;
	int ind = 0;
	int size = 0;
	float eachTime;
	float startTime = 0;
	float lastTime = 0;

	RenderTextureFormat? currentFormat = null;

	/// <summary>
	/// Captured RenderTexture size.
	/// </summary>
	public int CapturedImgSize { get { return ind; } }
	/// <summary>
	/// Captured RenderTextures.
	/// </summary>
	public RenderTexture[] CapturedBuffers { get { return buffers; } }
	#region ========================== Output Parameters ==========================
	public enum OutputType
	{
		JPG, PNG, EXR
	}
	[Header("=== Output Propertys ===")]
	public string outputFolder = string.Empty;
	[Header("Output Format")]
	public OutputType outputType = OutputType.PNG;

	[Header("JPG: Quality, set to unity default using -1")]
	public int JpgQuality = -1;
	[Header("Exr: Exr Type")]
	public Texture2D.EXRFlags exrType = Texture2D.EXRFlags.None;

	string lastOutputFolder = string.Empty;

	/// <summary>
	/// return last output folder path.
	/// </summary>
	public string FullOutputPath { get { return lastOutputFolder; } }
	#endregion
	#endregion

	#region ========================== Public Methods ==========================
	/// <summary>
	/// Pregenerate textures.
	/// </summary>
	public void GenerateBuffers()
	{
		if (captureFPS <= 0f)
		{
			Debug.LogError("Camera Capturer Error: generate failed, because captureFPS <= 0.");
			canCapture = false;
			return;
		}

		eachTime = 1f / (float)captureFPS;
		size = Mathf.CeilToInt(recordDuration / eachTime);

		if (buffers != null)
			buffers = ResizeArray(size, buffers);
		else
			buffers = new RenderTexture[size];

		Rect rect = fullScreen ? currentScreen : position;

		for (int c = 0, max = buffers.Length; c < max; ++c)
		{
			if (buffers[c] != null && ((buffers[c].width != (int)rect.width) || (buffers[c].height != (int)rect.height)))
			{
				DestroyImmediate(buffers[c]);
				buffers[c] = null;
			}

			if (buffers[c] == null)
				buffers[c] = new RenderTexture((int)rect.width, (int)rect.height, 32, currentFormat.Value, RenderTextureReadWrite.Default);

			if (!buffers[c].IsCreated())
				buffers[c].Create();
		}

		canCapture = true;
	}

	/// <summary>
	/// Start Capture. return false if buffer not generated. (GenerateBuffers() to generate buffer)
	/// </summary>
	/// <param name="time">Set current countdown time, set to -1 if you don't want to change it.</param>
	/// <returns></returns>
	public bool StartCapture(float time = -1f)
	{
		if (!canCapture) return false;
		result = false;
		if (time != -1f) CountdownTime = time;
		if (CountdownTime <= 0f)
			Restart();
		else
			CaptureCountDown();
		return true;
	}
	
	/// <summary>
	 /// Stop Capture.
	 /// </summary>
	public void StopCapture()
	{
		StopCapture(false);
	}

	/// <summary>
	/// Output results.
	/// </summary>
	public void OutputResults(bool useCoroutine = true)
	{
		if (result && processValue == null)
		{
			if (string.IsNullOrEmpty(outputFolder))
			{
				outputFolder = Application.dataPath + "/../Captured";
			}
			if (!System.IO.Directory.Exists(outputFolder))
			{
				Debug.Log("outputFolder not existed create : " + outputFolder);
				System.IO.Directory.CreateDirectory(outputFolder);
			}

			int count = 0;
			var now = System.DateTime.Now;
			string timeStr = now.Year.ToString() + (now.Month < 10 ? "0" : "") + now.Month.ToString() + (now.Day < 10 ? "0" : "") + now.Day.ToString();
			lastOutputFolder = outputFolder + "/" + timeStr + "/";
			while (System.IO.Directory.Exists(lastOutputFolder))
			{
				lastOutputFolder = outputFolder + "/" + timeStr + count++ + "/";
			}

			System.IO.Directory.CreateDirectory(lastOutputFolder);

			SaveAsTextures(useCoroutine);
		}
	}
	#endregion

	#region ========================== Private Methods ==========================
	/// <summary>
	/// Check for support.
	/// </summary>
	void OnEnable()
	{
		//Check Support
		int support = (int)(SystemInfo.copyTextureSupport & UnityEngine.Rendering.CopyTextureSupport.RTToTexture);
		if (support == 0)
		{
			Debug.LogError("SystemInfo.copyTextureSupport is not support for renderTexture to Texture!");
			enabled = false;
			return;
		}

		var cam = gameObject.GetComponent<Camera>();
		if (!cam)
		{
			Debug.LogWarning("ScreenToTexture cannot find camera on this gameObject, disabling");
			enabled = false;
			return;
		}
	}

	/// <summary>
	/// Destroy all buffer.
	/// </summary>
	void OnDisable()
	{
		if (buffers != null)
		{
			foreach (var tex in buffers)
			{
				if (tex != null)
					DestroyImmediate(tex);
			}
		}
	}
	/// <summary>
	/// Capture will do in here.
	/// </summary>
	void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		Graphics.Blit(source, destination);

		if (source != null && currentFormat == null)
			currentFormat = source.format;

		currentScreen.x = currentScreen.y = 0;
		if (currentScreen.width != source.width || currentScreen.height != source.height)
		{
			currentScreen.width = source.width;
			currentScreen.height = source.height;
			if (fullScreen)
				OnScreenSizeChange();
		}

		if (capturing)
		{
			float currentTime = Time.realtimeSinceStartup - startTime;
			float delta = currentTime - lastTime;

			if (delta >= eachTime)
			{
				Rect capture = new Rect(position.x, position.y, position.width, position.height);

				if (fullScreen)
					capture = new Rect(0, 0, (int)currentScreen.width, (int)currentScreen.height);
				else
					capture = ConvertToScreenSpace(capture);

				Graphics.CopyTexture(source, 0, 0, (int)capture.x, (int)capture.y, (int)capture.width, (int)capture.height, buffers[ind++], 0, 0, 0, 0);

				lastTime = currentTime;

				if (ind == size) StopCapture(true);
			}
		}
	}

	void MakeTextureAlphaChannel(Texture2D tex)
	{
		var pixels = tex.GetPixels32();
		for (int c = 0, max = pixels.Length; c < max; ++c)
		{
			Color32 colour = pixels[c];
			if (colour.a == 0)
			{
				colour.a = (byte)((colour.r + colour.g + colour.b) / 3);
				pixels[c] = colour;
			}
		}
		tex.SetPixels32(pixels);
	}

	void SaveAsTextures(bool useCoroutine)
	{
		if (useCoroutine)
			StartCoroutine(SaveAsTextureCoroutine());
		else
			SaveAsTexture();
	}

	IEnumerator SaveAsTextureCoroutine()
	{
		processValue = 0f;
		Texture2D texOut = null;
		for (int c = 0; c < ind; ++c)
		{
			var buffer = buffers[c];
			if (buffer != null)
			{
				if (texOut == null) texOut = new Texture2D(buffer.width, buffer.height, outputType == OutputType.EXR ? TextureFormat.RGBAFloat : TextureFormat.ARGB32, false);

				string path = lastOutputFolder + "capture" + c.ToString("D5");

				RenderTexture.active = buffer;
				texOut.ReadPixels(new Rect(0, 0, buffer.width, buffer.height), 0, 0);
				if (outputType == OutputType.PNG) MakeTextureAlphaChannel(texOut);
				texOut.Apply();

				byte[] bytes = null;
				switch (outputType)
				{
					case OutputType.JPG:
						path = path + ".jpg";
						if (JpgQuality == -1)
							bytes = texOut.EncodeToJPG();
						else
							bytes = texOut.EncodeToJPG(JpgQuality);
						break;
					case OutputType.EXR:
						path = path + ".exr";
						bytes = texOut.EncodeToEXR(exrType);
						break;
					default:
						path = path + ".png";
						bytes = texOut.EncodeToPNG();
						break;
				}
				if (System.IO.File.Exists(path))
					System.IO.File.Delete(path);
				System.IO.File.WriteAllBytes(path, bytes);

				processValue = (float)c / (float)ind;
				yield return null;
			}
		}
		DestroyImmediate(texOut);
		RenderTexture.active = null;

		processValue = 1f;
		yield return new WaitForSeconds(0.5f);
		processValue = null;
	}

	void SaveAsTexture()
	{
		Texture2D texOut = null;
		for (int c = 0; c < ind; ++c)
		{
			var buffer = buffers[c];
			if (buffer != null)
			{
				if (texOut == null) texOut = new Texture2D(buffer.width, buffer.height, outputType == OutputType.EXR ? TextureFormat.RGBAFloat : TextureFormat.ARGB32, false);

				string path = lastOutputFolder + "capture" + c.ToString("D5");

				RenderTexture.active = buffer;
				texOut.ReadPixels(new Rect(0, 0, buffer.width, buffer.height), 0, 0);
				if (outputType == OutputType.PNG) MakeTextureAlphaChannel(texOut);
				texOut.Apply();

				byte[] bytes = null;
				switch (outputType)
				{
					case OutputType.JPG:
						path = path + ".jpg";
						if (JpgQuality == -1)
							bytes = texOut.EncodeToJPG();
						else
							bytes = texOut.EncodeToJPG(JpgQuality);
						break;
					case OutputType.EXR:
						path = path + ".exr";
						bytes = texOut.EncodeToEXR(exrType);
						break;
					default:
						path = path + ".png";
						bytes = texOut.EncodeToPNG();
						break;
				}
				if (System.IO.File.Exists(path))
					System.IO.File.Delete(path);
				System.IO.File.WriteAllBytes(path, bytes);
			}
		}
		DestroyImmediate(texOut);
		RenderTexture.active = null;
	}

	#region ========================== Helper Methods ==========================
	/// <summary>
	/// Restart parameter for capture, and start.
	/// </summary>
	void Restart()
	{
		if (canCapture)
		{
			ind = 0;
			startTime = Time.realtimeSinceStartup;
			lastTime = 0;
			result = false;
			capturing = true;
		}
	}

	/// <summary>
	/// Stop capture.
	/// </summary>
	void StopCapture(bool done)
	{
		if (capturing)
		{
			capturing = false;
			FadeInSelection(1f, done);
			result = ind > 0;
		}
	}

	/// <summary>
	/// Start capture countdown timer/effect.
	/// </summary>
	void CaptureCountDown()
	{
		captureCountdownTimer = new FadeOut(CountdownTime);
		FadeOutSelection(CountdownTime);
	}

	/// <summary>
	/// Stop capturing and show log.
	/// </summary>
	void Reset(string reason)
	{
		if (capturing)
		{
			SetLog("Stop capturing because " + reason);
		}

		capturing = false;
		canCapture = false;
	}

	/// <summary>
	/// Called when application window (or Editor Game View) has resized.
	/// </summary>
	void OnScreenSizeChange()
	{
		Reset("screen size has changed.");
	}

	/// <summary>
	/// Creates a 1x1 texture in colour.
	/// </summary>
	static Texture2D CreateTextureInColour(Color32 colour)
	{
#if UNITY_2018 || UNITY_2018_1_OR_NEWER
		var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
#else
		var tex = new Texture2D(1, 1, TextureFormat.ARGB4444, false);
#endif
		tex.SetPixels32(new Color32[] { colour });
		tex.Apply(true);
		return tex;
	}

	/// <summary>
	/// Resize RenderTexture Array.
	/// </summary>
	static RenderTexture[] ResizeArray(int newSize, RenderTexture[] old)
	{
		RenderTexture[] next = new RenderTexture[newSize];
		if (old != null && old.Length > 0)
		{
			for (int c = 0, max = old.Length; c < max; ++c)
			{
				if (c < newSize)
					next[c] = old[c];
				else
					DestroyImmediate(old[c]);
			}
		}
		return next;
	}

	/// <summary>
	/// Convert screen space back to gui space.
	/// </summary>
	Vector2 ToRectSpace(Vector2 pos)
	{
		return new Vector2(pos.x, currentScreen.height - pos.y);
	}

	/// <summary>
	/// Convert rect from gui space to screen space for capturing.
	/// </summary>
	Rect ConvertToScreenSpace(Rect rect)
	{
		return new Rect(rect.x, Mathf.CeilToInt((int)currentScreen.height - rect.y - rect.height), rect.width, rect.height);
	}
	#endregion

	#endregion

	#region ========================== GUI Parameters ==========================
	struct FadeOut
	{
		readonly float time;
		public FadeOut(float duration = 3f)
		{
			time = Time.realtimeSinceStartup + duration;
		}

		public float Alpha
		{
			get { return Mathf.Clamp01(time - Time.realtimeSinceStartup); }
		}

		public float TimeLeft
		{
			get { return time - Time.realtimeSinceStartup; }
		}
	}
	const float buttonMoveTime = 0.5f;

	[HideInInspector] [System.NonSerialized] static Texture2D resizeTex = null;
	/// <summary>
	/// Texture for selected area.
	/// </summary>
	static Texture2D ResizeTex
	{
		get
		{
			if (resizeTex == null)
			{
				resizeTex = CreateTextureInColour(new Color32(112, 164, 200, 128));
			}
			return resizeTex;
		}
	}

	[HideInInspector] [System.NonSerialized] static Texture2D selectionTex = null;
	/// <summary>
	/// Texture for selected area.
	/// </summary>
	static Texture2D SelectionTex
	{
		get
		{
			if (selectionTex == null)
			{
				selectionTex = CreateTextureInColour(new Color32(167, 219, 255, 128));
			}
			return selectionTex;
		}
	}

	[HideInInspector] [System.NonSerialized] static Texture2D capturingTex = null;
	/// <summary>
	/// Texture for capturing area.
	/// </summary>
	static Texture2D CapturingTex
	{
		get
		{
			if (capturingTex == null)
			{
				capturingTex = CreateTextureInColour(new Color32(255, 219, 167, 64));
			}
			return capturingTex;
		}
	}

	[HideInInspector] [System.NonSerialized] static Texture2D processBarTex = null;
	/// <summary>
	/// Texture for capturing area.
	/// </summary>
	static Texture2D ProcessBarTex
	{
		get
		{
			if (processBarTex == null)
			{
				processBarTex = CreateTextureInColour(new Color32(255, 219, 167, 255));
			}
			return processBarTex;
		}
	}

	static GUIStyle redLabelStyle = null;
	/// <summary>
	/// Style for log.
	/// </summary>
	static GUIStyle RedLabelStyle
	{
		get
		{
			if (redLabelStyle == null)
			{
				redLabelStyle = new GUIStyle(GUI.skin.label);
				redLabelStyle.normal.textColor = Color.red;
				redLabelStyle.fontSize = 18;
			}
			return redLabelStyle;
		}
	}

	static GUIStyle bigLabelStyle = null;
	/// <summary>
	/// Style for countdown label.
	/// </summary>
	static GUIStyle BigLabelStyle
	{
		get
		{
			if (bigLabelStyle == null)
			{
				bigLabelStyle = new GUIStyle(GUI.skin.label);
				bigLabelStyle.normal.textColor = new Color(1f, 0.6f, 0f, 1f);
				bigLabelStyle.fontSize = 128;
			}
			return bigLabelStyle;
		}
	}

	static GUIStyle buttonStyle;
	/// <summary>
	/// Button style.
	/// </summary>
	static GUIStyle ButtonStyle
	{
		get
		{
			if (buttonStyle == null)
			{
				buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20, alignment = TextAnchor.MiddleLeft };
				buttonStyle.onNormal.background = buttonStyle.normal.background = CreateTextureInColour(new Color32(52, 52, 52, 128));
				buttonStyle.onHover.background = buttonStyle.hover.background = CreateTextureInColour(new Color32(128, 128, 128, 128));
				buttonStyle.onActive.background = buttonStyle.active.background = CreateTextureInColour(new Color32(255, 255, 255, 128));
				buttonStyle.onFocused.background = buttonStyle.focused.background = CreateTextureInColour(new Color32(255, 0, 0, 128));
			}
			return buttonStyle;
		}
	}

	string log = string.Empty;
	FadeOut logFadeOut;

	Color selectionColour = new Color(1.0f, 1.0f, 1.0f, 1.0f);
	FadeOut? selectionFadeOut;
	FadeOut? selectionFadeIn;
	bool drawDone = false;

	FadeOut? captureCountdownTimer;

	Vector2 buttonSize = new Vector2(120f, 50f);
	bool? isHovering = null;
	FadeOut buttonTimer;

	Vector2 resizeQuadSize = new Vector2(50f, 50f);
	Rect? resizeQuad;
	bool hoverOnResize = false;
	bool resizeDragging = false;

	Vector3 lastMousePos;
	bool pauseMouseEvent;

	bool unfullScreening = false;

	float? processValue = null;

	bool drawGUI = true;
	/// <summary>
	/// Toggle draw OnGUI
	/// </summary>
	public bool DrawGUI { get { return drawGUI; } set { drawGUI = value; } }
	#endregion

	#region ========================== GUI Methods ==========================
	/// <summary>
	/// Draw GUIs.
	/// </summary>
	private void OnGUI()
	{
		if (!drawGUI) return;
		// Selection texture.
		Rect rect = new Rect(position);
		if (!unfullScreening && fullScreen) rect = currentScreen;

		//Using GUI.Window for Dragging.
		var pos = GUI.Window(0, rect, WindowFunction, "", GUI.skin.label);
		if (!fullScreen && pos != rect)
		{
			position = pos;
			fullScreen = false;
			position.x = Mathf.Clamp(position.x, 0f, currentScreen.width - position.width);
			position.y = Mathf.Clamp(position.y, 0f, currentScreen.height - position.height);
			if (selectionFadeOut != null)
				selectionFadeOut = null;
			if (selectionFadeIn != null)
				selectionFadeIn = null;
			if (drawDone) drawDone = false;
			selectionColour.a = 1f;
			resizeQuad = null;
		}

		// Fade effect for selection texture.
		if (selectionFadeOut != null)
		{
			float texAlpha = selectionFadeOut.Value.Alpha;
			selectionColour.a = texAlpha;
			if (texAlpha == 0f)
				selectionFadeOut = null;
		}

		if (selectionFadeIn != null)
		{
			float texAlpha = selectionFadeIn.Value.Alpha;
			selectionColour.a = 1f - texAlpha;
			if (texAlpha == 0f)
			{
				selectionFadeIn = null;
				drawDone = false;
			}
		}

		GUI.color = selectionColour;

		if (!fullScreen && hoverOnResize)
		{
			Rect outline = new Rect(rect);
			float scaleEffect = buttonTimer.Alpha / buttonMoveTime;

			float size = Mathf.Lerp(10f, 0f, scaleEffect);
			outline.height += size;
			outline.width += size;

			GUI.DrawTexture(outline, ResizeTex);
		}

		// Draw selection texture.
		if (capturing)
			GUI.DrawTexture(rect, CapturingTex);
		else
			GUI.DrawTexture(rect, SelectionTex);

		if (drawDone)
			DrawBigLabelForWindow("Done", fullScreen ? currentScreen : position, false);

		GUI.color = Color.white;

		if (processValue != null)
		{
			float value = processValue.Value;
			Rect processBarRect = new Rect(rect.position.x, rect.position.y + rect.size.y - buttonSize.y, rect.size.x * value, buttonSize.y);
			GUI.DrawTexture(processBarRect, ProcessBarTex);
		}

		// Draw log.
		if (!string.IsNullOrEmpty(log))
		{
			float logAlpha = logFadeOut.Alpha;
			if (logAlpha == 0f)
				log = string.Empty;
			else
			{
				GUI.Label(rect, log, RedLabelStyle);
				var textColour = RedLabelStyle.normal.textColor;
				textColour.a = logAlpha;
				RedLabelStyle.normal.textColor = textColour;
			}
		}
		UpdateInput();
	}

	/// <summary>
	/// Draw Big Orange Label.
	/// </summary>
	void DrawBigLabelForWindow(string label, Rect windowRect, bool inWindowFunction)
	{
		int size = (int)Mathf.Min(windowRect.height, windowRect.width);
		if (size < 128)
			BigLabelStyle.fontSize = size;
		else
			BigLabelStyle.fontSize = 128;

		var labelSize = BigLabelStyle.CalcSize(new GUIContent(label));

		size = BigLabelStyle.fontSize;

		if (inWindowFunction)
		{
			windowRect.x = (windowRect.width / 2) - (labelSize.x / 2);
			windowRect.y = (windowRect.height / 2) - (size / 2);
		}
		else
		{
			windowRect.x += (windowRect.width / 2) - (labelSize.x / 2);
			windowRect.y += (windowRect.height / 2) - (size / 2);
		}

		windowRect.size = labelSize;

		GUI.Label(windowRect, label, BigLabelStyle);
	}

	/// <summary>
	/// Handle inputs.
	/// </summary>
	void UpdateInput()
	{
		if (!pauseMouseEvent)
		{
			Rect rectPos = ConvertToScreenSpace(fullScreen ? currentScreen : position);
			bool currentHovering = (rectPos.Contains(Input.mousePosition));
			if (isHovering != currentHovering)
			{
				isHovering = currentHovering;
				OnHoverWindow(currentHovering);
			}

			if (!capturing)
			{
				if (!fullScreen)
				{
					if (resizeQuad == null)
					{
						resizeQuad = new Rect(position.x + position.width - resizeQuadSize.x, position.y + position.height - resizeQuadSize.y, resizeQuadSize.x, resizeQuadSize.y);
					}

					Rect resizeRect = ConvertToScreenSpace(resizeQuad.Value);
					hoverOnResize = resizeRect.Contains(Input.mousePosition);
					if (hoverOnResize)
					{
						if (Input.GetMouseButtonUp(0))
						{
							resizeDragging = false;
							Resize();
						}
						else if (Input.GetMouseButtonDown(0))
						{
							resizeDragging = true;
						}
					}
					if (resizeDragging)
					{
						var currRect = ConvertToScreenSpace(currentScreen);
						if (!currRect.Contains(Input.mousePosition) || Input.GetMouseButtonUp(0))
						{
							resizeDragging = false;
							Resize();
						}
						else
						{
							var mousePos = ToRectSpace(Input.mousePosition);
							resizeQuad = new Rect(mousePos.x - resizeQuadSize.x, mousePos.y - resizeQuadSize.y, resizeQuadSize.x, resizeQuadSize.y);
							Resize();
						}
					}
				}
				else
				{
					if (Input.GetMouseButton(2))
					{
						var mousePos = ToRectSpace(Input.mousePosition);
						position.position = mousePos - (position.size * 0.5f);
						unfullScreening = true;
					}
					if (unfullScreening && Input.GetMouseButtonUp(2))
					{
						unfullScreening = false;
						fullScreen = false;
					}
				}
			}
		}
		if (lastMousePos != Input.mousePosition)
		{
			pauseMouseEvent = false;
			lastMousePos = Input.mousePosition;
		}
	}

	/// <summary>
	/// On Hover to capture window.
	/// </summary>
	void OnHoverWindow(bool hoverIn)
	{
		buttonTimer = new FadeOut(buttonMoveTime - buttonTimer.Alpha);
	}

	/// <summary>
	/// GUI.Window function.
	/// </summary>
	void WindowFunction(int id)
	{
		if (captureCountdownTimer != null)
		{
			float timeLeft = captureCountdownTimer.Value.TimeLeft;
			if (timeLeft <= 0f)
			{
				captureCountdownTimer = null;
				Restart();
			}
			else
			{
				Rect rectPos = fullScreen ? currentScreen : position;
				DrawBigLabelForWindow(Mathf.CeilToInt(timeLeft).ToString(), rectPos, true);
			}
		}

		Rect buttonRect = new Rect(Vector2.zero, buttonSize);
		float moveEffect = buttonTimer.Alpha / buttonMoveTime;
		if (isHovering.Value)
		{
			if (moveEffect != 0f)
				buttonRect.y = Mathf.Lerp(0f, -buttonSize.y, moveEffect);
		}
		else
		{
			if (moveEffect != 0f)
				buttonRect.y = Mathf.Lerp(-buttonSize.y, 0f, moveEffect);
			else
				buttonRect.y = -buttonSize.y;
		}

		string buttonText = "Prepare";
		if (capturing) buttonText = "Stop";
		else if (canCapture) buttonText = "Start";

		if (GUI.Button(buttonRect, buttonText, ButtonStyle))
		{
			if (capturing) StopCapture(false);
			else if (canCapture)
			{
				StartCapture();
				pauseMouseEvent = true;
				isHovering = false;
				OnHoverWindow(isHovering.Value);
			}
			else GenerateBuffers();
			buttonTimer = new FadeOut(buttonMoveTime);
		}

		if (result)
		{
			Rect resultButton = new Rect(buttonRect);
			resultButton.x += buttonRect.width + 5f;

			if (GUI.Button(resultButton, "Save Results", ButtonStyle))
			{
				OutputResults();
			}
		}

		if (!string.IsNullOrEmpty(lastOutputFolder))
		{
			Rect explorerButtone = new Rect(buttonRect);
			explorerButtone.x += buttonRect.width * 2 + 10f;

			if (GUI.Button(explorerButtone, "Explorer", ButtonStyle))
			{
				var itemPath = lastOutputFolder.Replace(@"/", @"\");   // explorer doesn't like front slashes
				System.Diagnostics.Process.Start("explorer.exe", "/select," + itemPath);
			}
		}

		if (!fullScreen)
		{
			float fullScreenButtonWidth = 40f;
			Rect fullScreenButton = new Rect(position.width - fullScreenButtonWidth, buttonRect.y, fullScreenButtonWidth, buttonSize.y);
			if (GUI.Button(fullScreenButton, " \u2750", ButtonStyle))
			{
				FullScreen = true;
			}
		}

		if (!hoverOnResize && !resizeDragging)
			GUI.DragWindow();
	}

	/// <summary>
	/// Resize capture window.
	/// </summary>
	void Resize()
	{
		Rect quad = resizeQuad.Value;
		float sizeX = quad.x + quad.width - position.x;
		float sizeY = quad.y + quad.height - position.y;
		position = new Rect(position.x, position.y, sizeX, sizeY);
		Reset("capture size has changed.");
	}

	/// <summary>
	/// Set log and apply fade effect.
	/// </summary>
	void SetLog(string message)
	{
		log = message;
		logFadeOut = new FadeOut(5f);
	}

	/// <summary>
	/// Set selection texture to fade out.
	/// </summary>
	void FadeOutSelection(float duration)
	{
		selectionFadeOut = new FadeOut(duration);
	}

	/// <summary>
	/// Set selection texture to fade in.
	/// </summary>
	void FadeInSelection(float duration, bool _drawDone)
	{
		if (selectionFadeOut != null)
		{
			duration -= selectionFadeOut.Value.TimeLeft;
			selectionFadeOut = null;
		}
		selectionFadeIn = new FadeOut(duration);
		drawDone = _drawDone;
	}
	#endregion

#if UNITY_EDITOR
	#region ========================== Editor OnVialidate Event ==========================
	bool? _fullScreen;
	Vector2? _size;
	float? _recordSecond;
	int? _captureFPS;
	private void OnValidate()
	{
		if (_fullScreen == null)
			_fullScreen = fullScreen;
		else
		{
			if (_fullScreen.Value != fullScreen)
			{
				_fullScreen = fullScreen;
				Reset("full screen mode has changed.");
			}
		}
		if (!fullScreen)
		{
			if (_size == null)
				_size = position.size;
			else
			{
				if (_size != position.size)
				{
					_size = position.size;
					Reset("capture size has changed.");
				}
			}
		}
		if (_recordSecond == null)
			_recordSecond = recordDuration;
		else
		{
			if (_recordSecond.Value != recordDuration)
			{
				_recordSecond = recordDuration;
				Reset("record duration has changed.");
			}
		}
		if (_captureFPS == null)
			_captureFPS = captureFPS;
		else
		{
			if (_captureFPS.Value != captureFPS)
			{
				_captureFPS = captureFPS = Mathf.Clamp(captureFPS, 1, captureFPS);
				Reset("FPS has changed.");
			}
		}
	}
	#endregion
#endif
}