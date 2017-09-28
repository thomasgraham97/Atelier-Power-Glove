using UnityEngine;
using System.Collections;

public class swell : MonoBehaviour 
{
	public float sinMult, sinRate;
	float value = 0f;
	void Update () 
	{
		value += (Time.deltaTime * sinRate);
		this.transform.localScale = Vector3.one + (Vector3.one * ( Mathf.Sin ( value ) * sinMult ) );

		if ( value > 360f ) { value = 0; }
	}
}
