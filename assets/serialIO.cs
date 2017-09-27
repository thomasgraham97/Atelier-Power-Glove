using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.IO.Ports;
using System;

/*
IO CODE GRATUITOUSLY STOLEN FROM
http://www.alanzucconi.com/2015/10/07/how-to-integrate-arduino-with-unity/
*/

[System.Serializable]
public struct changeableObject
{
	public GameObject subject;
	public Texture2D[] textures;
}

[System.Serializable]
public struct inputCombination
{
	public string name;
	public int[] fingerIndices; 		//KEY: 0: Index; 1: Middle; 2: Ring; 3: Pinkie;
	public float[] intensities;
	public changeableObject affiliatedObject;
}

[System.Serializable]
public struct confirmedInput
{
	public int fingerIndex;
	public float intensity;
}

public class serialIO : MonoBehaviour
 {
	SerialPort arduinoPort; //Class for communicating with individual port
	public inputCombination[] inputCombinations;
	List <confirmedInput> confirmedInputs = new List<confirmedInput>();
	int previousComboIndex;

	public float signalThreshold; public int signalTimeThreshold;
	int [] candidateInputTimes = new int[4];

	public int port;

	void Start ()
	{
		arduinoPort = new SerialPort("COM" + port.ToString(), 9600); //That port is COM4, all comms done at 9600 baud.
		arduinoPort.ReadTimeout = 50;				//The timeout for read requests is now set at 50ms.
		arduinoPort.Open();							//Open the serial port stream.

		StartCoroutine ( readPins() );
	}

	void OnDestroy ()
	{
		Debug.Log ("Serial IO closing COM" + port.ToString() + ".");
		arduinoPort.Close();
	}

	IEnumerator readPins ()
	{
		while (true)
		{
			float[] signals = new float[4];
			confirmedInputs = new List<confirmedInput>();

			//SEND READ REQUESTS, STORE RESPONSE
			sendString ("R0");					//Send out a call for the ping command.
			StartCoroutine ( readString ( (string result) => { signals[0] = float.Parse (result); }, () => Debug.Log ("No response from COM" + port +"."), 10f ) );
			sendString ("R1");					//Send out a call for the ping command.
			StartCoroutine ( readString ( (string result) => { signals[1] = float.Parse (result); }, () => Debug.Log ("No response from COM" + port +"."), 10f ) );
			sendString ("R2");					//Send out a call for the ping command.
			StartCoroutine ( readString ( (string result) => { signals[2] = float.Parse (result); }, () => Debug.Log ("No response from COM" + port +"."), 10f ) );
			sendString ("R3");					//Send out a call for the ping command.
			StartCoroutine ( readString ( (string result) => { signals[3] = float.Parse (result); }, () => Debug.Log ("No response from COM" + port +"."), 10f ) );

			//FILTER RESPONSES
			for ( int index = 0; index < 4; index++ )
			{
				if (signals [ index] > signalThreshold )	{	candidateInputTimes [ index ]++; 	}
				else { candidateInputTimes [ index ] = 0; }

				if ( candidateInputTimes [ index ] > signalTimeThreshold ) 
				{
					confirmedInput newConfirmed = new confirmedInput();
					newConfirmed.fingerIndex = index; newConfirmed.intensity = signals [ index ];
					confirmedInputs.Add ( newConfirmed );
				}
			}

			foreach (confirmedInput confirmed in confirmedInputs)
			{
				Debug.Log ("confirmed input at index " + confirmed.fingerIndex );
			}

			//COMPARE WITH DEFINED COMBINATIONS
			int comboIndex = 14;

			for ( int index = 0; index < inputCombinations.Length; index++ )
			{
				for ( int fingerIndex = 0; fingerIndex < inputCombinations [index].fingerIndices.Length; fingerIndex++ )
				{
					for ( int matchIndex = 0; matchIndex < confirmedInputs.Count; matchIndex++ )
					{
						if ( confirmedInputs [ matchIndex ].fingerIndex != inputCombinations [ index ].fingerIndices [ fingerIndex ] )	{	comboIndex = 14; break;	} //Break if non-match
						if ( matchIndex == confirmedInputs.Count - 1 ) { comboIndex = index;	}						//If we get to the end,
					}
				}
			}

			/*//TRANSFER PRESS INTENSITY
			if ( comboIndex != 14 )
			{
				inputCombinations [ comboIndex ].intensities = new float [ inputCombinations [ comboIndex ].fingerIndices.Length ];
				for ( int index = 0; index < inputCombinations [ comboIndex ].fingerIndices.Length; index++ )
				{
					inputCombinations [ comboIndex ].intensities [ index ] = confirmedInputs [ index ].intensity;
				}
			}*/

			//DETECT HOLD
			if ( previousComboIndex == comboIndex && comboIndex != 14 )
			{
				growAsset ( inputCombinations [ comboIndex ].affiliatedObject.subject, new float[1] );
			}
			else if ( previousComboIndex != comboIndex && comboIndex != 14 ) 
			{ changeAsset ( inputCombinations [ comboIndex ].affiliatedObject.subject , new float[1] ); }

			previousComboIndex = comboIndex;

			yield return new WaitForSeconds (0.01f);
		}
	}

	public void sendString ( string message ) 	//Sends string information to the Arduino.
	{
		arduinoPort.WriteLine ( message );		//Send the string.
		arduinoPort.BaseStream.Flush();			//Prevents IOExceptions.
	}

	//Callback: Function, defined by invoker. Read result is used as argument. //Fail: Function, defined by invoker.
	//Timeout: Wait time before ending read request.
	public IEnumerator readString ( Action <string> callback, Action fail = null, float timeout = float.PositiveInfinity )
	{
		DateTime initialTime = DateTime.Now, currentTime;	//initialTime: Time when enumerator was called. //currentTime: Time at a given enumerator tick.
		TimeSpan timeDifference = default ( TimeSpan );		//The difference between the above variables.

		string dataString = null;							//Contents of read.

		do
		{
			try { dataString = arduinoPort.ReadLine(); }									//Try to read from the serial port.
			catch ( TimeoutException ) { dataString = null; }								//If the read request times out, then set the read contents to nothing.

			if ( dataString != null )	{	callback ( dataString ); yield return null; }	//If there's something in the read contents, run the defined function, and end the enumerator.
			else {	yield return new WaitForSeconds (0.05f);	}							//Otherwise, wait an extra 5ms.

			currentTime = DateTime.Now;														//Update the current tick time.
			timeDifference = currentTime - initialTime;										//Calculate the difference since starting.
		}	while ( timeDifference.Milliseconds < timeout );								//Stop running this function if it takes too long.

		if ( fail != null && dataString == null ) { fail(); }	yield return null;			//If the invoker specified a fail event, invoke it and end the enumerator.	
	}

	void changeAsset ( GameObject subject, float[] intensities )
	{
		float intensity = 0f;	foreach (float value in intensities) { intensity +=value; }	intensity = intensity / intensities.Length;
		Debug.Log ("changing asset " + subject.name + " at intensity " + intensity );
	}

	void growAsset ( GameObject subject, float[] intensities )
	{
		float intensity = 0f;	foreach (float value in intensities) { intensity +=value; }	intensity = intensity / intensities.Length;
		Debug.Log ("growing asset " + subject.name + " at intensity " + intensity );
	}
}