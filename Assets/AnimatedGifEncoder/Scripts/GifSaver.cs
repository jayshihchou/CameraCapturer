using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GifSaver : MonoBehaviour
{
	public CameraCapturer cameraCapturer = null;

	private void OnEnable()
	{
		cameraCapturer = gameObject.GetComponent<CameraCapturer>();
		if (cameraCapturer == null)
		{
			enabled = false;
			Debug.LogWarning("Please attach GifSaver to Camera that contains CameraCapturer");
		}
	}

	bool saving = false;
	float processValue = 0f;

	[Header("Gif: manual set fps. (set 0 use same as captureFPS)")]
	public float gifFPS = 0f;
	[Header("Gif: repeat times. (set 0 means play forever)")]
	public int gifRepeatTime = 0;
	[Header("Gif: output cull alpha? (Note: Gif does not support alpha channel)")]
	public bool gifCullAlpha = false;

	IEnumerator SaveGifCoroutine()
	{
		saving = true;
		var e = new Gif.Components.AnimatedGifEncoder();

		e.Start();
		e.SetDispose(gifCullAlpha ? 2 : 0);
		e.SetTransparent(Color.clear);
		e.SetFrameRate(gifFPS == 0f ? cameraCapturer.CaptureFPS : gifFPS);
		e.SetRepeat(gifRepeatTime);

		Texture2D tex = null;
		int size = cameraCapturer.CapturedImgSize;
		var buffers = cameraCapturer.CapturedBuffers;
		for (int c = 0; c < size; ++c)
		{
			var buffer = buffers[c];
			if (buffer != null)
			{
				if (tex == null) tex = new Texture2D(buffer.width, buffer.height, TextureFormat.ARGB32, false);
				RenderTexture.active = buffer;

				tex.ReadPixels(new Rect(0, 0, buffer.width, buffer.height), 0, 0);
				tex.Apply();

				e.AddFrame(tex);
			}

			processValue = (float)c / (float)size;
			if (c % 2 == 0)
				yield return null;
		}

		e.Finish();

		System.IO.MemoryStream ms = e.Output();

		var fs = new System.IO.FileStream(cameraCapturer.FullOutputPath + "capture.gif", System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.Write, System.IO.FileShare.None);
		fs.Write(ms.ToArray(), 0, (int)ms.Length);
		fs.Close();

		if (tex != null) DestroyImmediate(tex);
		RenderTexture.active = null;

		processValue = 1f;
		yield return new WaitForSeconds(0.5f);
		saving = false;
	}

	void SaveGif()
	{
		saving = true;
		var e = new Gif.Components.AnimatedGifEncoder();

		e.Start();
		e.SetDispose(gifCullAlpha ? 2 : 0);
		e.SetTransparent(Color.clear);
		e.SetFrameRate(gifFPS == 0f ? cameraCapturer.CaptureFPS : gifFPS);
		e.SetRepeat(gifRepeatTime);

		Texture2D tex = null;
		int size = cameraCapturer.CapturedImgSize;
		var buffers = cameraCapturer.CapturedBuffers;
		for (int c = 0; c < size; ++c)
		{
			var buffer = buffers[c];
			if (buffer != null)
			{
				if (tex == null) tex = new Texture2D(buffer.width, buffer.height, TextureFormat.ARGB32, false);
				RenderTexture.active = buffer;

				tex.ReadPixels(new Rect(0, 0, buffer.width, buffer.height), 0, 0);
				tex.Apply();

				e.AddFrame(tex);
			}

			processValue = (float)c / (float)size;
		}

		e.Finish();

		System.IO.MemoryStream ms = e.Output();

		var fs = new System.IO.FileStream(cameraCapturer.FullOutputPath + "capture.gif", System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.Write, System.IO.FileShare.None);
		fs.Write(ms.ToArray(), 0, (int)ms.Length);
		fs.Close();

		if (tex != null) DestroyImmediate(tex);
		RenderTexture.active = null;

		processValue = 1f;
		saving = false;
	}

	public void SaveGif(bool useCoroutine = true)
	{
		if (useCoroutine)
		{
			StartCoroutine(SaveGifCoroutine());
		}
		else
		{
			SaveGif();
		}
	}

	private void OnGUI()
	{
		if (!saving && GUILayout.Button("Save Gif"))
		{
			SaveGif();
		}
		else if (saving)
		{
			GUILayout.Label("Saving Gif :" + processValue.ToString() + "%");
		}
	}
}