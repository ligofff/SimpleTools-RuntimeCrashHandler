# Runtime Crash Handler
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)


## About
Runtime Crash Handler - A small asset for Unity, that catch up all game exceptions and errors, stops game and show window with options.<br />

<p align="center">
  <img width="800" src="https://user-images.githubusercontent.com/44195161/230169674-55394c54-e43a-4859-8871-b34a2e83dfc2.png">
</p>

> [My Telegram channel](https://t.me/ligofff_blog) if you want more

## Overview
Crashes in the game are bad. But even worse are hidden crashes.

When something breaks in your game, an unexpected null appears somewhere or something else is unforeseen in the code - if you do not stop the game, the errors can begin to overlap each other and spoil the gameplay very much, generating more and more bugs.<br />
And the player will continue to play the game, and not understand why it does not work as it should.<br />
And if your game has a save system, and the game is saved "broken", then the player can permanently lose his progress in the game, which will lead him to incredible frustration.

**Therefore, to avoid such situations, this tool was created.**<br />
If something in the game does not go according to plan, and a log like "Error" or "Exception" gets into the console, the game stops and a window is displayed with a description of the error and possible options.<br />
You can write to a player that game is broken and it will no longer behave as expected. Or you can prevent the player from playing further in the game altogether, leaving only the "Exit" button *(This is what I recommend doing)*.<br />

Also, you can add any other options, such as "Send a report", or "Take a screenshot", etc - the asset lends itself very well to modifications, I made all the necessary methods virtual.

*There is also an option that puts all "Errors" as notifications on the side, and does not stop the game. This can be useful when you know for sure that bugs in your code won't cascade destroy everything.*

## Minimum Requirements
* I think everything will be fine on Unity 2020 and above, but tested only for 2022.2

### Install via GIT URL
Go to ```Package Manager``` -> ```Add package from GIT url...``` -> Enter ```https://github.com/ligofff/SimpleTools-RuntimeCrashHandler.git``` -> Click ```Add```

You will need to have Git installed and available in your system's PATH.

## Usage

After install, you will find ```RuntimeExceptionsHandler``` folder in your ```Packages``` folder.
Open it, then go to ```Runtime``` -> ```Prefabs``` and just drop ```RuntimeExceptionsHandler``` prefab to your scene.

<p align="center">
  <img width="700" src="https://user-images.githubusercontent.com/44195161/230177030-95b7d18a-7af6-4f48-83d6-76467eb07d01.png">
</p>

**Thats all!** Try to broke something in your game now.

*If you need to add your own logic to the behavior of an asset, then simply inherit from one of the three available classes, and change what you need.*

## License

MIT License

Copyright (c) 2022 Ligofff

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
