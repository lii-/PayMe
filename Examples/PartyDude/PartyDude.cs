using System;
using System.Windows;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
//using System.IO;
using System.Threading;

using Zeta;
using Zeta.Common;
using Zeta.TreeSharp;
using Zeta.Common.Plugins;
using Zeta.CommonBot.Profile;
using Zeta.CommonBot.Profile.Common;
using Zeta.CommonBot;
using Zeta.XmlEngine;
using Action = Zeta.TreeSharp.Action;
using Zeta.CommonBot.Settings;
using Zeta.Internals.Actors;
using Zeta.Internals.SNO;


/*
	PartyDude is to be used in conjuction with PartyLeader, and enables your bots to run together in a group, owning, looting and raping all
	that comes before them.
	
	PartyDude is for non leaders of the group, and should be placed in the PLUGINS folder of the DB associated with your char.
	Each char that is not the leader needs to have a copy of PartyDude
	
	There is nothing you need to configure, it is a plug and kick butt plugin.
	
	Author: ChuckyEgg
	Inspired by and based on the BreakTaker plugin, Author: no1knowsy
	Date: 18th of August, 2012
	Verion: 0.1
	
 */
namespace PartyDude
{
    public class PartyDude : IPlugin
    {
		// used to generate a random value to represent the time the char will stop for a spliff, and for how long the char will take to smoke it
		private Random randomNumber = new Random();
		private int randomTime = 0;
		
		// this is used to ensure that the toon pauses for a random amount of time when in town
		private bool notInParty = true;


        // DO NOT EDIT BELOW THIS LINE IF YOU DO NOT KNOW WHAT YOU ARE DOING
        // -----------------------------------------------------------------
        // To those who do, feel free :)
        #region Iplugin

        public void Log(string message, LogLevel level = LogLevel.Normal) 
		{ 
			Logging.Write(level, string.Format("{0}", message)); 
		}
		
        public bool Equals(IPlugin other)
        {
            return other.Name == Name;
        }

        public string Author
        {
            get { return "ChuckyEgg"; }
        }

        public string Description
        {
            get { return "F this, it's time for a spliff"; }
        }

        public string Name
        {
            get { return "PartyDude"; }
        }

        public Version Version
        {
            get { return new Version(0, 1); }
        }

		// I did not write the line below. One thing's for sure, the guy needs a toke on a big fat dooby ;-)
		
		// There is no config GUI for this part of the plugin
		public Window DisplayWindow
        {
            get
            {
                return null;
            }
        }

        /// <summary> Executes the shutdown action. This is called when the bot is shutting down. (Not when Stop() is called) </summary>
        public void OnShutdown()
        {
        }		
		
        public void OnEnabled()
        {
            Zeta.CommonBot.GameEvents.OnGameChanged += GameChanged;
            Logging.Write("Bot away, dude!");
        }

        /// <summary> Executes the disabled action. This is called when he user has disabled this specific plugin via the GUI. </summary>
        public void OnDisabled()
        {			
            Zeta.CommonBot.GameEvents.OnGameChanged -= GameChanged;
            Log("Disabled!");
        }

        public void OnInitialize()
        {
			// set up new values for ActualBottingPeriod and ActualSpliffTime, store the current time (representing the time the farming starts)
			Initialise_All();
        }
		

        #endregion

        public void OnPulse()
        {
            // Set up button objct to represent the buttons in the game window
			// this can be used for any button, but only one button at a time
            Zeta.Internals.UIElement Button = null;	
			// Wait for invite
			if (Zeta.Internals.UIElement.IsValidElement(0x9EFA05648195042D) && (Button = Zeta.Internals.UIElement.FromHash(0x9EFA05648195042D)) != null)
			{
				// is the invite graphic visible
                if (Button.IsVisible)
                {
					Log("Invite exists");
					// invite has shown up, accept it
					Log("Wait for a few seconds");
					randomTime = (randomNumber.Next(1, 3) * 1000);
					Thread.Sleep(randomTime);
					Button.Click();
				
					// Confirm joining party (click OK)
					if (Zeta.Internals.UIElement.IsValidElement(0xB4433DA3F648A992) && (Button = Zeta.Internals.UIElement.FromHash(0xB4433DA3F648A992)) != null)
					{		
						// is the joining party confirmation button visible
						if (Button.IsVisible)
						{
								// invite has shown up, accept it
								Log("Wait for a few seconds");
								randomTime = (randomNumber.Next(1, 3) * 1000);
								Log("Clicking the OKAY button");
								// click ok
								Button.Click();
						}
					}
				}
								
				// wait for a bit in order to allow the leader to finish creating the party
				Thread.Sleep(5000);
					
			}
			
			// Follower leaving message
			// need to OKAY this in order to get rid of it
            if (Zeta.Internals.UIElement.IsValidElement(0xF85A00117F5902E9) && (Button = Zeta.Internals.UIElement.FromHash(0xF85A00117F5902E9)) != null)
            {
                if (Button.IsVisible)
                {
					Log("Follower is leaving message has popped up");
					// click on the OK button
					Button.Click();
                }
            }
			// Follower is joining up again - if you are the only one in the party, then might as well have the follower rejoin
			// need to OKAY this in order to get rid of it
            if (Zeta.Internals.UIElement.IsValidElement(0x161745BBCE22A8BA) && (Button = Zeta.Internals.UIElement.FromHash(0x161745BBCE22A8BA)) != null)
            {
                if (Button.IsVisible)
                {
					Log("Follower is joining message has popped up");
					// click on the YES button
					Button.Click();
                }
            }
        } // END OF OnPulse()

        /*
            This is executed whenever a new game is created
        */
        public void GameChanged(object sender, EventArgs e)
        {
            Log("New game!");

			// pause for a few seconds (3 to 6), while bot checks for missing members
            Log("Wait for the leader to create a new game");
			randomTime = (randomNumber.Next(3, 6) * 1000);
            Thread.Sleep(randomTime);
				
			Log("Reloading the profile");
			// Reload profile
			Zeta.CommonBot.ProfileManager.Load(GlobalSettings.Instance.LastProfile);
		} // END OF GameChanged(object sender, EventArgs e)

		/*
			This method initialises a number of variables to the starting values required when the char starts on a new run after a break or
			when DB is first run
		 */
        private void Initialise_All()
        {
			// initialisation of variables goes here
        } // END OF Initialise_All()
		
    }

}
