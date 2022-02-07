﻿# OBS ANT+ Hear Rate Monitor
> A small tool to display your heart rate in OBS using a ANT+ hear rate sensor.

## Motivation
When I was looking up existing solutions I found that they were either very limited to specific sensors or very convoluted by using a phone and a cloud service.\
This tool needs nothing more than absolutely needed, a sensor and a USB dongle, and should be compatible with every sensor with ANT+ support.

## Hardware
### USB Dongle
To be able to receive the sensor values a ANT+ USB dongle is needed.\
This tool was coded and tested with the official "Garmin ANT+ USB-Stick" (also known as "ANT USB-m Stick") but you can find a lot of different dongles in various online stores.
It was also testes with a cheap dongle "USB ANT STICK U1" from "CYCPLUS".\
As long as it registers with the VID 0x0FCF and the PID 0x1008 or 0x1009 it should work.
### Hear Rate Sensor
Every sensor that is ANT+ compatible should work. This tool was coded and tested with a cheap "H8 Heart Rate Monitor" from "COOSPO".

## Device Drivers
Use [Zadig](https://zadig.akeo.ie) to install the libusb-win32 driver for the device.\
The drivers I found online are all based on libusb-win32 but were outdated so using Zadig to install the latest version is probably the better option.

## Usage
### Tool Setup
At first the sensor id needs to be known. Start the tool and press the "Search for Sensor" button. In the newly opened window wait until a sensor is found, copy its id an close the window.\
Paste the id into the "Sensor ID" field. Configure the OSB Text Log if wanted and then press "Connect". If the Log feature is enabled a text file with the same name as the application will be written which contains the last received heart rate.\
The selected settings will be saved and loaded automatically.\
*Note: Setting the Sensor ID to 0 will connect to any sensor (wildcard id). However it should be a lot quicker to directly address the sensor with the correct id.*

### OBS Setup
Create a new "Text (GDI+)" source in the scene that should display the heart rate. In its configuration window select "Read from file" and select the log .txt file generated by the tool.
