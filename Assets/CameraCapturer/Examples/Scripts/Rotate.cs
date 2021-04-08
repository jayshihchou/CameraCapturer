using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotate : MonoBehaviour {

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		var euler = transform.eulerAngles;
		euler.y += Time.deltaTime * 50f;
		transform.eulerAngles = euler;
	}
}
