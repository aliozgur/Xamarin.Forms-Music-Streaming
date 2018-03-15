﻿using Android.App;
using System;
using Android.Content;
using Android.Media;
using Android.Net;
using Android.Net.Wifi;
using Android.OS;
using Android.Graphics;
using System.Net;
using Xamarin.Forms;
using Android.Runtime;
using Android.Media.Session;

namespace Radio021.Droid
{
    [Service]
    [IntentFilter(new[] { ActionPlay, ActionPause, ActionStop, ActionTogglePlayback, ActionNext, ActionPrevious })]
    public class StreamingBackgroundService : Service, AudioManager.IOnAudioFocusChangeListener
    {
        //Actions
        public const string ActionPlay = "com.xamarin.action.PLAY";
        public const string ActionPause = "com.xamarin.action.PAUSE";
        public const string ActionStop = "com.xamarin.action.STOP";
        public const string ActionTogglePlayback = "com.xamarin.action.TOGGLEPLAYBACK";
        public const string ActionNext = "com.xamarin.action.NEXT";
        public const string ActionPrevious = "com.xamarin.action.PREVIOUS";

        private const string Mp3 = App.Stream_URL;

        public static MediaPlayer player;
        public static bool isBuffering = true;
        private AudioManager audioManager;
        private WifiManager wifiManager;
        private WifiManager.WifiLock wifiLock;
        private RemoteControlClient remoteControlClient;
        private ComponentName remoteComponentName;
        private bool paused;


        /// <summary>
        /// On create simply detect some of our managers
        /// </summary>
        public override void OnCreate()
        {
            
            base.OnCreate();

            
            //Find our audio and notificaton managers
            audioManager = (AudioManager)GetSystemService(AudioService);
            wifiManager = (WifiManager)GetSystemService(WifiService);

            remoteComponentName = new ComponentName(PackageName, new RemoteControlBroadcastReceiver().ComponentName);
        }

        /// <summary>
        /// Will register for the remote control client commands in audio manager
        /// </summary>
        private void RegisterRemoteClient()
        {
            try
            {
                if (remoteControlClient == null)
                {
                    audioManager.RegisterMediaButtonEventReceiver(remoteComponentName);
                    //Create a new pending intent that we want triggered by remote control client
                    var mediaButtonIntent = new Intent(Intent.ActionMediaButton);
                    mediaButtonIntent.SetComponent(remoteComponentName);
                    // Create new pending intent for the intent
                    var mediaPendingIntent = PendingIntent.GetBroadcast(this, 0, mediaButtonIntent, 0);
                    // Create and register the remote control client
                    remoteControlClient = new RemoteControlClient(mediaPendingIntent);
                    audioManager.RegisterRemoteControlClient(remoteControlClient);
                }


                //add transport control flags we can to handle
                remoteControlClient.SetTransportControlFlags(RemoteControlFlags.Play |
                    RemoteControlFlags.Pause);


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        /// <summary>
        /// Unregisters the remote client from the audio manger
        /// </summary>
        private void UnregisterRemoteClient()
        {
            try
            {
                audioManager.UnregisterMediaButtonEventReceiver(remoteComponentName);
                audioManager.UnregisterRemoteControlClient(remoteControlClient);
                remoteControlClient.Dispose();
                remoteControlClient = null;
                

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }



        /// <summary>
        /// Don't do anything on bind
        /// </summary>
        /// <param name="intent"></param>
        /// <returns></returns>
        public override IBinder OnBind(Intent intent)
        {
            return null;
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {

            switch (intent?.Action)
            {
                case ActionPlay: Play(); break;
                case ActionStop: Stop(); break;
                case ActionPause: Pause(); break;
                case ActionTogglePlayback:
                    if (player == null)
                        return StartCommandResult.Sticky;

                    if (player.IsPlaying)
                        Stop();
                    else
                        Play();
                    break;
            }

            //Set sticky as we are a long running operation
            return StartCommandResult.Sticky;
        }

        /// <summary>
        /// Intializes the player.
        /// </summary>
        private void IntializePlayer()
        {
            player = new MediaPlayer();


            //Tell our player to sream music
            player.SetAudioStreamType(Stream.Music);

            //Wake mode will be partial to keep the CPU still running under lock screen
            player.SetWakeMode(ApplicationContext, WakeLockFlags.Partial);

            //When we have prepared the song start playback
            player.Prepared += (sender, args) =>
            {
                if (remoteControlClient != null)
                    remoteControlClient.SetPlaybackState(RemoteControlPlayState.Playing);
                
                player.Start();
            };

            //When we have reached the end of the song stop ourselves, however you could signal next track here.
            player.Completion += (sender, args) =>
            {
                Stop();
            };
            player.Info += (sender, e) =>
            {
                switch (e.What)
                {
                    case MediaInfo.BufferingEnd:
                        isBuffering = false;
                        break;
                    case MediaInfo.BufferingStart:
                        isBuffering = true;
                        break;
                    default:
                        break;
                }
            };
            player.Error += (sender, args) =>
            {
                Stop();
                Play();
            };

            
        }

        private async void Play()
        {
            isBuffering = true;
            if (paused && player != null)
            {
                paused = false;
                //We are simply paused so just start again
                player.Start();

                //Update remote client now that we are playing
                RegisterRemoteClient();
                remoteControlClient.SetPlaybackState(RemoteControlPlayState.Playing);
                return;
            }

            if (player == null)
            {
                IntializePlayer();
            }

            if (player.IsPlaying)
                return;

            try
            {
                await player.SetDataSourceAsync(ApplicationContext, Android.Net.Uri.Parse(Mp3));

                var focusResult = audioManager.RequestAudioFocus(this, Stream.Music, AudioFocus.Gain);
                if (focusResult != AudioFocusRequest.Granted)
                {
                    //could not get audio focus
                    Console.WriteLine("Could not get audio focus");
                }

                player.PrepareAsync();
                AquireWifiLock();
                //Update the remote control client that we are buffering
                RegisterRemoteClient();
                remoteControlClient.SetPlaybackState(RemoteControlPlayState.Buffering);
            }
            catch (Exception ex)
            {
                //unable to start playback log error
                Console.WriteLine("Unable to start playback: " + ex);
            }
        }


        private void Pause()
        {
            if (player == null)
                return;

            if (player.IsPlaying)
                player.Pause();


            remoteControlClient.SetPlaybackState(RemoteControlPlayState.Paused);
            StopForeground(true);
            paused = true;
        }

        private void Stop()
        {
            if (player == null)
                return;

            if (player.IsPlaying)
            {
                player.Stop();
                if (remoteControlClient != null)
                    remoteControlClient.SetPlaybackState(RemoteControlPlayState.Stopped);
            }

            player.Reset();
            player = null;
            paused = false;
            isBuffering = false;
            StopForeground(true);
            ReleaseWifiLock();
            UnregisterRemoteClient();
        }




        /// <summary>
        /// Lock the wifi so we can still stream under lock screen
        /// </summary>
        private void AquireWifiLock()
        {
            if (wifiLock == null)
            {
                wifiLock = wifiManager.CreateWifiLock(WifiMode.Full, "xamarin_wifi_lock");
            }
            wifiLock.Acquire();
        }

        /// <summary>
        /// This will release the wifi lock if it is no longer needed
        /// </summary>
        private void ReleaseWifiLock()
        {
            if (wifiLock == null)
                return;

            wifiLock.Release();
            wifiLock = null;
        }


        /// <summary>
        /// Properly cleanup of your player by releasing resources
        /// </summary>
        public override void OnDestroy()
        {
            base.OnDestroy();
            if (player != null)
            {
                player.Release();
                player = null;
            }
        }

        /// <summary>
        /// For a good user experience we should account for when audio focus has changed.
        /// There is only 1 audio output there may be several media services trying to use it so
        /// we should act correctly based on this.  "duck" to be quiet and when we gain go full.
        /// All applications are encouraged to follow this, but are not enforced.
        /// </summary>
        /// <param name="focusChange"></param>
        public void OnAudioFocusChange(AudioFocus focusChange)
        {
            switch (focusChange)
            {
                case AudioFocus.Gain:
                    if (player == null)
                        IntializePlayer();

                    if (!player.IsPlaying)
                    {
                        player.Start();
                        paused = false;
                    }

                    player.SetVolume(1.0f, 1.0f);//Turn it up!
                    break;
                case AudioFocus.Loss:
                    //We have lost focus stop!
                    Stop();
                    break;
                case AudioFocus.LossTransient:
                    //We have lost focus for a short time, but likely to resume so pause
                    Pause();
                    break;
                case AudioFocus.LossTransientCanDuck:
                    //We have lost focus but should till play at a muted 10% volume
                    if (player.IsPlaying)
                        player.SetVolume(.1f, .1f);//turn it down!
                    break;

            }
        }

    }

}
