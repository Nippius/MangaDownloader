MangaDownloader
===============

A little command line program used to download manga chapters from http://www.mangastream.to/
This is a little app i made to learn how to use the TPL in .Net .

How to use
===============

Step 1:
	Compile the code using Visual Studio (tested with Visual Studio 2013 and .Net Framework 4.5)

Step 2:
	Open the "App.config" file and find the line that reads <add key="urlManga" value="naruto"/>

Step 3:
	Edit the "value" property of step 2 and change it to the manga that you want to download.
	To find correct values to use you will need to read the url of one of the images from the
		intended manga and figure out wich part is needed.

Step 4:
	Simply launch the app and wait for it to finish.