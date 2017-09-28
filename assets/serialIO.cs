using UnityEngine;
using UnityEngine.UI;						//To run dashboard, for COM Port UI
using System.Collections.Generic;			//To store confirmed pressed fingers in a list
using System.Collections;
using System.IO.Ports;						//To access USB Ports using .NET
using System;

/**************************************************************************
USB CODE DERIVED FROM ALAN ZUCCONI's TUTORIAL
http://www.alanzucconi.com/2015/10/07/how-to-integrate-arduino-with-unity/
**************************************************************************/

//CHANGEABLE OBJECT: Links a subject game object to a set of textures that can be applied to it.
[System.Serializable]	public struct changeableObject
	{	public GameObject subject;	public Texture2D[] textures;	}

//INPUT COMBINATION: Links a combination of finger presses with a subject game object. Also stores press intensitiess for each digit.
[System.Serializable]	public struct inputCombination
{
	public string name;
	public int[] fingerIndices; 		//KEY: 0: Index; 1: Middle; 2: Ring; 3: Pinkie;
	public float[] intensities;
	public changeableObject affiliatedObject;
}

//CONFIRMED INPUT: Links a finger index with the intensity of the press. Declared when signal passes threshold tests.
public struct confirmedInput
	{	public int fingerIndex;	public float intensity;	}

public class serialIO : MonoBehaviour
 {
	SerialPort arduinoPort;														//Class for communicating with individual port
	public inputCombination[] inputCombinations;								//List of potential input combinations. Defined in editor.
	int previousComboIndex;														//The previous recorded input combination's index in the above list.
	List <confirmedInput> confirmedInputs = new List<confirmedInput>();			//A list of currently-confirmedi nputs. Variable in size.

	public float signalThreshold; 					//Editor-defined amount of input recognized as signal.
	public int signalTimeThreshold;					//Editor-defined. Number of coroutine cycles until input confirmed as signal.
	int [] candidateInputTimes = new int[4];		//Keeps track of each finger's consecutive passes of signalThreshold test. Provides data for signalTimeThreshold.

	public Text port, _index, middle, ring, pinkie;			//UI. "_index" because "index" is a common name and a cause for confusion.
	Text[] textArray;

	void Start ()
	{
		textArray = new Text[] { _index, middle, ring, pinkie }; //UI. Can be defined now that constituent objects are defined.

		for (int index = 0; index < inputCombinations.Length; index++)	//Create a random face on startup.
			{	changeAsset ( inputCombinations [ index ].affiliatedObject, new float[1] { UnityEngine.Random.Range (512, 768f) } );	}
	}																												//^ These numbers are defined between 0-1023.
																													//This is to match the Arduino's analog inputs.
	public void startPort ()	//Called by "Connect" button.
	{
		if (arduinoPort == null)											//If we haven't set up a port yet...
		{
			arduinoPort = new SerialPort("COM" + port.text, 9600); 			//Start a new port according to the port text field's contents.
			arduinoPort.ReadTimeout = 40;									//The timeout for read requests is now set at 40ms. Should match coroutine cycle rate.
			arduinoPort.Open();												//Open the serial port stream.

			StartCoroutine ( readPins() );									//Start reading the contents of the serial stream.
		}
		else { Debug.Log ("Port already open."); }
	}

	void OnDestroy ()	//Called when this component is destroyed.
	{
		if (arduinoPort != null)											//If there was a port to begin with
		{	if ( arduinoPort.IsOpen )										//and that port is open...
				{	Debug.Log ("Serial IO closing COM" + port.text + ".");	arduinoPort.Close();	}	//Close it.
		}
	}

	IEnumerator readPins ()
	{
		while (true)
		{
			for ( int index = 0; index < 4; index++)								//Reset font styles.
			{	textArray [ index ].fontStyle = FontStyle.Normal; }

			int comboIndex = 99;		//Dummy combination index. Replaced if combination made.
			float[] signals = new float[4];
			confirmedInputs = new List<confirmedInput>();

			//SEND READ REQUESTS, STORE RESPONSE
			sendString ("R0");					//Send out a call to read pin A0. Sent as string over USB.
			StartCoroutine ( readString ( (string result) => { signals[0] = float.Parse (result); }, 10f ) );
			sendString ("R1");	//^ The coroutine is readString.
			StartCoroutine ( readString ( (string result) => { signals[1] = float.Parse (result); }, 10f ) );
			sendString ("R2");				//^ A miniature namespace. Sets result as output.
			StartCoroutine ( readString ( (string result) => { signals[2] = float.Parse (result); }, 10f ) );
			sendString ("R3");																		//^ What to do 
			StartCoroutine ( readString ( (string result) => { signals[3] = float.Parse (result); }, 10f ) );

			_index.text = "Index signal: " + signals[0].ToString();		//Update UI.
			middle.text = "Middle signal: " + signals[1].ToString();
			ring.text = "Ring signal: " + signals[2].ToString();
			pinkie.text = "Pinkie signal: " + signals[3].ToString();

			//FILTER RESPONSES
			for ( int index = 0; index < 4; index++ )			//For each finger,
			{													//Perform a signal threshold check. If it passes, add to the input time counter.
				if (signals [ index] > signalThreshold )	{	candidateInputTimes [ index ]++; 	}
				else { candidateInputTimes [ index ] = 0; }											//If it fails, reset the counter.

				if ( candidateInputTimes [ index ] > signalTimeThreshold ) 							//If the counter passes the time threshold test,
				{
					confirmedInput newConfirmed = new confirmedInput();									//Confirm a new input.
					newConfirmed.fingerIndex = index; newConfirmed.intensity = signals [ index ];		//Promote signal data to confirmed input.
					confirmedInputs.Add ( newConfirmed );												//Add it to the list of confirmed inputs.
				}
			}

			if (confirmedInputs.Count > 0)																	//If any outputs were confirmed this cycle...
			{
				for ( int index = 0; index < confirmedInputs.Count; index++)								//Run through the confirmed inputs.
					{	textArray [ confirmedInputs[index].fingerIndex ].fontStyle = FontStyle.Bold; }		//Bold their UI displays.

				//COMPARE WITH DEFINED COMBINATIONS
				for ( int index = 0; index < inputCombinations.Length; index++ )
				{
					for ( int fingerIndex = 0; fingerIndex < inputCombinations [index].fingerIndices.Length; fingerIndex++ )
					{
						for ( int matchIndex = 0; matchIndex < confirmedInputs.Count; matchIndex++ )
						{
							if ( confirmedInputs [ matchIndex ].fingerIndex != inputCombinations [ index ].fingerIndices [ fingerIndex ] )	{	comboIndex = 99; break;	} //Break if non-match
							if ( matchIndex == confirmedInputs.Count - 1 ) { comboIndex = index; break;	}						//If we get to the end,
						}
					}
				}

				/*//TRANSFER PRESS INTENSITY  -- broken, spoofed instead
				if ( comboIndex != 14 )
				{
					inputCombinations [ comboIndex ].intensities = new float [ inputCombinations [ comboIndex ].fingerIndices.Length ];
					for ( int index = 0; index < inputCombinations [ comboIndex ].fingerIndices.Length; index++ )
					{
						inputCombinations [ comboIndex ].intensities [ index ] = confirmedInputs [ index ].intensity;
					}
				}*/

				//DETECT HOLD
				if ( previousComboIndex == comboIndex && comboIndex != 99 ) //If the last detected combination and the current combination are the same, and the current combination is not a null value...
					{ growAsset ( inputCombinations [ comboIndex ].affiliatedObject, new float[1] );	}	//This is a hold event. Grow the associated asset.
				else if ( previousComboIndex != comboIndex && comboIndex != 99 ) //If the last detected combination i bsn't the current combination, and the current combination is not a null value...
					{ changeAsset ( inputCombinations [ comboIndex ].affiliatedObject, new float[1] );	}	//This is a press event. Change the associated asset.
			}

			previousComboIndex = comboIndex;
			yield return new WaitForSeconds (0.16f);
		}
	}

	public void sendString ( string message ) 	//Sends string information to the Arduino.
	{
		arduinoPort.WriteLine ( message );		//Send the string.
		arduinoPort.BaseStream.Flush();			//Prevents IOExceptions.
	}

	//Callback: Function, defined by invoker. Read result is used as argument.	//Timeout: Wait time before ending read request.
	public IEnumerator readString ( Action <string> callback, float timeout = float.PositiveInfinity )
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
		}	while ( timeDifference.Milliseconds < timeout );								//Stop running this coroutine if it takes too long.

		yield return null;																	//Exit the coroutine.
	}

	void changeAsset ( changeableObject subject, float[] intensities )
	{
		subject.subject.transform.localScale = Vector3.one;
		float intensity = 0f;	foreach (float value in intensities) { intensity +=value; }	intensity = intensity / intensities.Length; //Average press intensities
		subject.subject.GetComponent<MeshRenderer>().material.mainTexture = subject.textures [ UnityEngine.Random.Range (0, subject.textures.Length) ]; //Texture swap.
		subject.subject.transform.localScale = Vector3.one * Mathf.Clamp ( 1.5f * (intensity / 1024), 0.5f, 1.5f );	//Intensity spoofing.
		Debug.Log ("changing asset " + subject.subject.name + " at intensity " + intensity );

	}

	void growAsset ( changeableObject subject, float[] intensities )
	{
		float intensity = 0f;	foreach (float value in intensities) { intensity +=value; }	intensity = intensity / intensities.Length;
		subject.subject.transform.localScale += (Vector3.one * (intensity / 1023) * 0.1f); //Intensity spoofy. Add to the object's scale.
		Debug.Log ("growing asset " + subject.subject.name + " at intensity " + intensity );
	}

	void Update() //DEBUG INPUT SPOOFING
	{
		if (Input.GetKey (KeyCode.Q) )	
		{ 
			confirmedInput newConfirmed  = new confirmedInput();
			newConfirmed.fingerIndex = 3; newConfirmed.intensity = UnityEngine.Random.Range (0, 1023);
			confirmedInputs.Add ( newConfirmed ); 
		}

		if (Input.GetKey (KeyCode.W) )	
		{ 
			confirmedInput newConfirmed  = new confirmedInput();
			newConfirmed.fingerIndex = 2; newConfirmed.intensity = UnityEngine.Random.Range (0, 1023);
			confirmedInputs.Add ( newConfirmed ); 
		}

		if (Input.GetKey (KeyCode.E) )	
		{ 
			confirmedInput newConfirmed  = new confirmedInput();
			newConfirmed.fingerIndex = 1; newConfirmed.intensity = UnityEngine.Random.Range (0, 1023);
			confirmedInputs.Add ( newConfirmed ); 
		}

		if (Input.GetKey (KeyCode.R) )	
		{ 
			confirmedInput newConfirmed  = new confirmedInput();
			newConfirmed.fingerIndex = 0; newConfirmed.intensity = UnityEngine.Random.Range (0, 1023);
			confirmedInputs.Add ( newConfirmed ); 
		}

		if (Input.GetKey (KeyCode.T ) ) //DEBUG RANDOM ASSET CHANGE
		{
			changeAsset ( inputCombinations [ UnityEngine.Random.Range (0, inputCombinations.Length) ].affiliatedObject, new float[] { UnityEngine.Random.Range (512, 768 ) } );
		}
	}
}