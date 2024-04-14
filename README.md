# ModPlayer
.net application that plays Amiga mod files.
Supported format is MOD organized sound module files using various channel count.

# Key features of the player
* Support MOD file format with different number of channels
* Implementation of all known effects
* Option to set the output quality
  * Playback frequency, for example 22050, 44100,
  * Number of channels - mono, stereo, stereo pan and surround
  * Bit depth - 8, 16
* Option to set the global volume level and change it in realtime
* The option to turn on will turn off individual channels from mixing to the input stream
* The stream can be redirected to a WAV file and determine how many milliseconds will be exported.
* It is possible to export instruments to WAV files
* You can set where the mod file starts playing.
* Playback of only the selected pattern or the entire song.
* Player can be run as `IHostedService` - background service in .net core
* The `IModPlayer` interface defines the methods that can be used.

# Inspiration
I wanted to be able to use the mod player for my own projects. Either for demo projects or intended games.
I have always been attracted to understanding how things work, and having my own implementation seems to me to be a great opportunity to learn new things, to push my own boundaries and to be able to overcome obstacles of complexity or ignorance.

For the implementation, I was inspired by several source codes that I found on the Internet.
One of the nicely written ones is smod player which is written in C++ by Mark Feldman (c) 1997.
Although it does not implement a lot of effects, it contains some bad interpretations of the documentation and some implementation variants are dependent on compilation directives, despite this, it is very well designed, understandable and well structured.
To the author Mark Feldman, I bow and thank you for opensource.
In return, I'm posting my code, which is written in C# and is designed for .net 8 but is easily convertible to older versions of .net. The differences will be in the syntax version of the language I used.

# Implementation history
I programmed in Delphi for many years before moving to .net. In this language I wrote in 11/2001, the first version inspired by smod C++ aplication and its wave stream callback architecture.
At the time, I was trying to create a mod player that would be able to play mod files in real time, with the most accurate implementation of ekects.
I used Delphi mod player in several demo applications in which I demonstrated simple 3D graphics accompanied by music produced by this mod player.
Speaking of dates, the first version was written in Delphi in 11/2001 and the last version in 04/2024 in C# (C++++).

# Debuging and tuning effects
The correct implementation is not exactly described anywhere and you need to try how the effects work and how they are used.
Because even though there are several source codes in different languages, they are all different and some are poorly implemented.

It is good to proceed in such a way that someone who understands orpavedu explains how the effect works and shows the work with the effect on a concrete example.
Ideally, he will create a mod with one sample that will demonstrate how the effect works.
Then I compare how my implementation sounds and tweak it so that I am satisfied with the result against the original.
I declared that the original would be playback from the Protracker application.

Such videos form the wasp amiga youtube channel, which is of high quality and I recommend watching it.
Here is a playlist dedicated to working with Protracker:
https://www.youtube.com/watch?v=S1yE2qL8UcY&list=PLVoRT-Mqwas9gvmCRtOusCQSKNQNf6lTc

On non-Amiga machines I recommend using Eightbitbubsy's Protracker clone: http://16-bits.org/pt.php (Windows/Mac)
For enthusiasts who still use a real uAmiga or an emulator, for example WinUAE, here is the real thing: http://aminet.net/package/mus/edit/pt23d (Amiga)
                            
There are several ways to convert a song in mod format to MP3 or WAV.
I use Bassoon Tracker: https://www.stef.be/bassoontracker/ because it seems to me that its playback reproduction is the most faithful to the original of the available players.

## Vibrato testování
Vibrato is a periodic variation in pitch. It is achieved by modulating the frequency of the note. The frequency of the vibrato is determined by the vibrato speed and the depth of the vibrato is determined by the vibrato depth. The vibrato speed is the number of times per second the pitch.

Vibrato demo video:
https://www.youtube.com/watch?v=2iSy8HYwRVU

Based on the video, I created several mod files, which I then converted to WAV via Bassoon Tracker.
The files are stored in the directory: ModPlayer/Mods/TestFiles/Vibrato/
The WAV file format is used to compare how vibrato works in the ModPlayer application against the original.
              
# MOD files as a playback quality test
I attached to the project a mod of songs by different authors, which I used to test the playback quality.
They are always available for download in Amiga MOD archives such as mod archie: https://modarchive.org/

I have chosen songs that I relate to. My first computer was the Atari 800 XL, the next was the Motorola family, the Amiga 1200.
This computer is still an icon today and I'm glad I had the opportunity to work with it, play games on it, watch the demo scene, learn how to rip, etc.

# How to use the player?
To make it clear what methods the player supports, I pulled them into the interface `IModPlayer` with comments.
My goal was to create a player that would be able to play as many modules as possible that are available on the Internet, and most importantly, it was able to play them cheaply from the point of view of computing power.
This implementation was helped by the great NAudio library that I used to achieve my goals.
            
