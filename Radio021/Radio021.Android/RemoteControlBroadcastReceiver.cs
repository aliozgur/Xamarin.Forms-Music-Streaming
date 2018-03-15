﻿
using Android.App;
using Android.Content;
using Android.Media;
using Android.Views;

namespace Radio021.Droid
{
    [BroadcastReceiver]
    [Android.App.IntentFilter(new[] { Intent.ActionMediaButton })]
    public class RemoteControlBroadcastReceiver : BroadcastReceiver
    {

        /// <summary>
        /// gets the class name for the component
        /// </summary>
        /// <value>The name of the component.</value>
        public string ComponentName { get { return this.Class.Name; } }

        /// <Docs>The Context in which the receiver is running.</Docs>
        /// <summary>
        /// When we receive the action media button intent
        /// parse the key event and tell our service what to do.
        /// </summary>
        /// <param name="context">Context.</param>
        /// <param name="intent">Intent.</param>
        public override void OnReceive(Context context, Intent intent)
        {


            if (intent.Action != Intent.ActionMediaButton)
                return;

            //The event will fire twice, up and down.
            // we only want to handle the down event though.
            var key = (KeyEvent)intent.GetParcelableExtra(Intent.ExtraKeyEvent);
            if (key.Action != KeyEventActions.Down)
                return;

            var action = StreamingBackgroundService.ActionPlay;

            switch (key.KeyCode)
            {
                case Keycode.Headsethook:
                case Keycode.MediaPlayPause:
                    action = StreamingBackgroundService.ActionTogglePlayback;
                    break;
                case Keycode.MediaPlay:
                    action = StreamingBackgroundService.ActionPlay;
                    break;
                case Keycode.MediaPause:
                    action = StreamingBackgroundService.ActionPause;
                    break;
                case Keycode.MediaStop:
                    action = StreamingBackgroundService.ActionStop;
                    break;
                case Keycode.MediaNext:
                    action = StreamingBackgroundService.ActionNext;
                    break;
                case Keycode.MediaPrevious:
                    action = StreamingBackgroundService.ActionPrevious;
                    break;
                default:
                    return;
            }

            var remoteIntent = new Intent(action);
            context.StartService(remoteIntent);
        }
    }

    /// <summary>
    /// This is a simple intent receiver that is used to stop playback
    /// when audio become noisy, such as the user unplugged headphones
    /// </summary>
    [BroadcastReceiver]
    [Android.App.IntentFilter(new[] { AudioManager.ActionAudioBecomingNoisy })]
    public class MusicBroadcastReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            if (intent.Action != AudioManager.ActionAudioBecomingNoisy)
                return;

            //signal the service to stop!
            var stopIntent = new Intent(StreamingBackgroundService.ActionStop);
            context.StartService(stopIntent);
        }
    }

}
