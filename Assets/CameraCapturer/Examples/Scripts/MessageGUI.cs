using UnityEngine;

public class MessageGUI : MonoBehaviour
{
	[TextArea]
	public string text;

	Rect windowPos = new Rect(15f, 15f, 300f, 300f);

	private void Start()
	{
		windowPos.x = Screen.width - 300f - 15f;
		windowPos.y = Screen.height - 300f - 15f;
	}

	private void OnGUI()
	{
		windowPos = GUI.Window(-10, windowPos, Window, "How To :");
	}

	void Window(int id)
	{
		GUI.Label(new Rect(5, 13, windowPos.width - 30f, windowPos.height - 30f), text);
		GUI.DragWindow();
	}
}