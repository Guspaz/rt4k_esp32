# Readme

## Table of Contents
- [Installing/Updating](#install)
- [Connecting to wifi](#wifi)
- [General Use](#general)
- [Using WebDAV](#webdav)
- [Future Plans](#future)

<a id='install'></a>
### Installing/Updating
- These instructions are for Windows. MacOS and Linux are supported, check here for instructions: https://github.com/nanoframework/nanoFirmwareFlasher
- Using the SD-WIFI-PRO dev board, set the "RD UPLD" dipswitch (2) to the "UPLD" (ON) position
- Plug the SD-WIFI-PRO into the dev board and connect the dev board to your PC
- Open a command prompt
- If you have not already, install the "nanoff" tool by typing: <pre>dotnet tool install -g nanoff</pre>
	- You can update the "nanoff" tool later by typing: <pre>dotnet tool update -g nanoff</pre>
- If you have not already, figure out which COM port it connected to by running: <pre>nanoff --listports</pre>
	- If you see multiple COM ports listed, follow these instructions on Windows: https://github.com/nanoframework/nanoFirmwareFlasher#finding-the-device-com-port-on-windows
- From the same directory as the rt4k\\\_esp32 bin files
	- Install the base firmware with the following command: <pre>nanoff --update --target ESP32\\\_PSRAM\\\_REV3 --serialport COM3 --clrfile nanoCLR.bin
	- Deploy the rt4k\\\_esp32 application with the following command: <pre>nanoff --deploy --target ESP32\\\_PSRAM\\\_REV3 --serialport COM3 --image rt4k\\\_esp32.bin</pre>
- Unplug the SD card and plug it in again, either with the dev board in SD reader mode, or into your RT4K.

<a id='wifi'></a>
### Connecting to wifi
- Prepare (in advance) a "wifi.ini" file with the following format:
	- <pre>ssid = &lt;ssid\_here&gt;
password = &lt;password\_here&gt;</pre>
- Connect the RT4K to a PC using its dev board with the "SD ESP" dipswitch (1) is set to "SD" (OFF) and the "RD UPLD" dipswitch (2) is set to "RD" (OFF)
- On a fresh install, you will have 30 seconds to copy the "wifi.ini" file to the root of the SD card
	- After 30 seconds, the ESP32 will try to read the wifi.ini file, which will cause Windows to disconnect it.
	- You can change this timer on the settings page of the web UI (the delay is to give the RT4K time to boot or do firmware updates, I hope to be able to remove the delay in the future after some RT4K firmware changes)
- When the ESP32 boots, it will immediately try to connect to the wifi network in its internal cache. After 30 seconds, it will read the wifi.ini file, and if it's different from the cache, it will update the internal cache and reboot.
- After the ESP32 connects to the wifi, it will write its IP address to "ipAddress.txt" in the root of the SD card. You can either look at your router to try to find the IP address (it will be listed as "nanodevice_&lt;something&gt;"), or you can connect the SD card to a PC to read the ipAddress.txt file (it will probably get the same IP address again when it reconnects). I suggest setting up a static IP in your router configuration so that you always know the IP address.


<a id='general'></a>
### General Use
- The ESP32 will start booting the instant the SD card gets power.
    - The RT4K always powers the SD slot even when the RT4K is off, as long as the RT4K power cable is plugged in.
- Any time the ESP32 accesses the SD card (read or write), it will temporarily disconnect the SD card from the host device (such as the RT4K or Windows)
    - Windows hates this and will unmount the SD card until you physically unplug/replug it
    - The RT4K is fine with it, as long as it's not booting or updating the firmware
- In order to avoid interfering with the RT4K boot or firmware updates, the ESP32 will refuse to try to access the SD card for the first 30 seconds after it boots
	- You can change the timer on the settings page
	- You can disable the locking and have it just delay reading the wifi.ini file, since that also disrupts RT4K boot/firmware updates.
- **WARNING!** Avoid opening the "images" folder over WebDAV! Windows will try to generate thumbnails and WebDAV will slow to a crawl!
- **WARNING!** WebDAV is very slow! If it's too slow, use the SD card on a PC with the "Disable wifi next boot" button:
    - Click the button
    - Wait for the confirmation
    - Unplug the SD card from the RT4K (or other device)
    - Plug the SD card into its reader connected to your PC
        - If using the SD-WIFI-PRO dev board as your reader, make sure the "SD ESP" dipswitch (1) is set to "SD" (OFF) and the "RD UPLD" dipswitch (2) is set to "RD" (OFF)
    - The SD card will now be mounted to your Windows machine and you can move stuff around with high performance
    - Unplug the SD card and plug it back into the RT4K. It should now boot back up again with wifi enabled.

<a id='webdav'></a>
### Using WebDAV
- A very basic (written from scratch) WebDAV server is running on port 81.
- These instructions assume Windows 11. I imagine that Windows 10 will be similar.
- To access the contents of the SD card using WebDAV, open a new Windows Explorer window, right click inside the window and select "Add a network location".
	- In the wizard that follows, copy the WebDAV address from the ESP32 status page to the Windows wizard, and give the network location an appropriate name.
- You can also map a drive letter to the WebDAV server exactly the same way you would map a network drive over SMB (Windows File Sharing), only use the WebDAV address for the folder field.

<a id='future'></a>
### Future Plans/ideas (in no particular order)
- Add buttons on the timings calculator to write to the modeline files automatically
- Remove the startup delay for SD access/wifi.ini access
	- This requires the RT4K to implement retries for reading the firmware on RT4K boot
		- Mike said he can do it when things get less hectic
	- Once we do this, there may need to be a special "update firmware" option to avoid breaking firmware updates
	- Probably can simplify the wifi code a bunch once this is done?
- Design an RT4K filesystem API
	- Basic idea is, send commands to the RT4K using special folders/files on the SD card
	- Lets us tell the RT4K to create or load a profile
	- Obviously requires Mike to implement on the RT4K, goal is to keep this extremely simple/easy for him
- Full support for RT4K profiles
	- Need to get full profile struct typedef from Mike
- Support changing arbitrary RT4K settings via the web ui
	- Requires RT4K API
	- Basic idea is, tell RT4K to dump current settings to a profile, read the profile from the SD, user changes settings in UI, write to profile file, tell RT4K to load profile.
- Overlay profiles
	- For example, a *.rt4 file as a base, and some sort of human-readable files to override parts of that, merged on the fly
	- For example, game-specific tweaks to console-specific profiles
- Move RT4K profile checksum calculation from C# to C++
	- C# is very slow on ESP32, calculating the checksum of RT4K profiles is slow.
	- Implementing the checksum function in C++ in the .NET nanoFramework base firmware would speed this up greatly
- Add C++ support for ZIP files
	- C# is too slow to do anything useful with ZIP files, could implement in nanoFramework base firmware instead
	- This would enable things like automatic RT4K firmware updates or automatic profile sync
- Support translating RT4K profiles to/from human readable file format
	- Probably JSON since it's the only serialization format supported by nanoFramework
- Auto reboot after long idle time?
	- In case there are any long-term stability issues
- WebDAV eTag support
	- To maybe help Windows avoid useless operations
- Client-side validation for settings page
	- Current implementation reverts all settings if any of them are wrong
- WebDAV should return 500 on error
	- Right now it might just not complete the operation or disconnect