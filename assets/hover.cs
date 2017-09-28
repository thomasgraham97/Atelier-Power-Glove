using UnityEngine;
using System.Collections;

public class hover : MonoBehaviour {

	Vector3 startpos; public float magnitude, rate;
	void Start () {
		startpos = this.transform.position;
	}
	
	// Update is called once per frame
	void Update () {
		Vector3 goalpos = startpos + (Random.insideUnitSphere * magnitude);
		this.transform.position = Vector3.Lerp ( this.transform.position, goalpos, Time.deltaTime * rate );
	}
}
