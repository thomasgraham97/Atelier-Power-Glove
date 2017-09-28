//Code derived from a tutorial by Alan Zucconi. Available at http://www.alanzucconi.com/2015/10/07/how-to-integrate-arduino-with-unity/
#include <SerialCommand.h> //Library by Steven Cogswell. Available at https://github.com/scogswell/ArduinoSerialCommand

SerialCommand serialCommand;  //A class that handles commands regarding the serial port.
int pinReading0, pinReading1, pinReading2, pinReading3, pinReading4, currentPin;
  //All pin reading values, and the pin to be read.
byte pins[] = { A0, A1, A2, A3 }; //An array of the pins to be read.

void setup() 
{  
  pinMode ( A0, INPUT );  pinMode ( A1, INPUT );  pinMode ( A2, INPUT );  pinMode ( A3, INPUT );
    //Set all pins to input.
  
  Serial.begin (9600); //Begin interacting with the serial port.
  serialCommand.addCommand ("PING", pingHandler ); //HANDLERS: These statements declare commands triggered by strings set over the serial port.
  serialCommand.addCommand ("R0", pin0Handler );
  serialCommand.addCommand ("R1", pin0Handler );
  serialCommand.addCommand ("R2", pin0Handler );
  serialCommand.addCommand ("R3", pin0Handler );
}

void loop() 
{
  if ( Serial.available() > 0 )  {    serialCommand.readSerial();  }
  if ( currentPin > 3 ) { currentPin = 0; } //Cycles through analog pins one at a time to prevent "ghosting." Learned through https://github.com/firmata/arduino/issues/334
  pinReading0 = analogRead (pins[currentPin]);
  currentPin++;
  delay(5);
}

/*****************************************************************************************
|HANDLER COMMANDS: SerialCommand associates these with strings sent over the serial port.|
*****************************************************************************************/
void pingHandler()  {Serial.println ("PONG");}
void pin0Handler()   {Serial.println ( pinReading0 );}
void pin1Handler()   {Serial.println ( pinReading1 );}
void pin2Handler()   {Serial.println ( pinReading2 );}
void pin3Handler()   {Serial.println ( pinReading3 );}
