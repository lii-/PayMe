using System;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Windows.Markup;

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
	PartyLeader is used in conjunction with PartyDude. This PartyLeader file is used by the char who will be the leader of the group, and should be placed
	in their PLUGINS folder
	
	When in town, the char will attempt to create a group. It will make 3 attempts to create the party, allowing for the other chars to 
	get to a point where they can accept an invite.
	
	Default setting allows for a 4 character party, and for the leader to make regular checks on the integrity of the party (seeing if anyone has left)
	
	The Party Leader part of the plugin has a configuration GUI, allowing the changing of:
	1) The total number of people in the party
	2) Whether or not the leader should make regular checks on the party integrity
	
	Author: ChuckyEgg
	Half Inched Code: GilesSmith's cofigeration window code from GilesCombatReplacer plugin
	Date: 18th of August, 2012
	Verion: 1.0
	
	Half Inched - nicked, ripped off :P ;) :)
	
 */
namespace PartyLeader
{
    public class PartyLeader : IPlugin
    {
		// used to generate a random value to make the action appear more natural
		private Random randomNumber = new Random();
		private int randomTime = 0;
		
        // this represents the total number of people that will make up the party
        // this can be 2, 3 or 4
        private int totalNumberOfPartyMembers = 2;
		
        // These are used for checking if anyone has left the party. Check made every couple of minutes
		// --------------------------------------------------------------------------------------------

        // this is used to allow the bot to check if anyone has left the party
        // if set to true, it will make the check, otherwise it will not
        // if someone has left the party, a new game will be created
        private bool enablePartyCheck = true;
		
		// The time of the last check on the party state (did anyone leave the party)
        private DateTime lastPartyCheckTime;

        // the time gap between checks for party dropouts
        // 2 = 2 minutes
        private int checkOnPartyTimeGap = 2;

        // this is used as a flag to signify if a member of the party has left
        // if someone has left, then we must create a new game
        private bool partyMemberHasLeft = false;

        // This flag is used to test for multiple game creations within a short period of time, just incase someone has
        // left the group and unable to coe back, and nobody is around to alter totalNumberOfPartyMembers to reflect
        // this change in the total number of party members
        private int numberOfGameCreationsInTheLastTenMins = 0;
        private DateTime gameCreationMonitorStartTime;

		// Config window variables/identifiers/objects
		// -------------------------------------------
		// Config window controls
        private RadioButton check2DudeParty, check3DudeParty, check4DudeParty;
        private CheckBox checkParty;
		private Button btnDone, btnCheckPartyIntegrity;
		// The config window object
        private Window configWindow;
		
		private bool check2DudePartySetting, check3DudePartySetting, check4DudePartySetting;


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
            get { return "Party on, Dude!"; }
        }

        public string Name
        {
            get { return "PartyLeader"; }
        }

        public Version Version
        {
            get { return new Version(0, 1); }
        }

        /// <summary> Executes the shutdown action. This is called when the bot is shutting down. (Not when Stop() is called) </summary>
        public void OnShutdown()
        {
        }
        public void OnEnabled()
        {
			// When activated, this is used to invite the people to the party
            Zeta.CommonBot.GameEvents.OnGameChanged += GameChanged;
			// Not actually used at the moment, but to be made use of.... possibly
            Zeta.CommonBot.GameEvents.OnGameLeft += GameLeft;
			// set default values to certain variables, identifiers, objects, etc.
            Initialise_All();
            Logging.Write("Bot away, dude!");
            Log("Enabled!");
        }

        /// <summary> Executes the disabled action. This is called when he user has disabled this specific plugin via the GUI. </summary>
        public void OnDisabled()
        {
            Zeta.CommonBot.GameEvents.OnGameChanged -= GameChanged;
            Zeta.CommonBot.GameEvents.OnGameLeft -= GameLeft;
            Log("Disabled!");
        }

        public void OnInitialize()
        {
        }
		
        #endregion

        public void OnPulse()
        {
            // Create a button object to represent the buttons on the screen. This is used for one button at a time.
            Zeta.Internals.UIElement Button = null;

            // check how many games were created in the last 10 minutes
            // if 3 or more games were created, then head back to base and stop this bot
            if (enablePartyCheck && ((DateTime.Now - gameCreationMonitorStartTime).TotalMinutes >= 10))
            {
                if (numberOfGameCreationsInTheLastTenMins >= 3)
                {
                    Log("DANGER, DANGER, Will Robinson!");
                    Log("Too many game creations within the last 10 minutes!");
                    Log("Someone may have left permenantly!");
                    // TP back to base
                    ZetaDia.Actors.Me.UseTownPortal();
                    // stop the bot
                    BotMain.Stop();
                }
                // reset, ready for the next 10 minutes
                numberOfGameCreationsInTheLastTenMins = 0;
            }
			
            // Check to see if anyone has left the party. This is done every checkOnPartyTimeGap minutes
            if (enablePartyCheck && ((DateTime.Now - lastPartyCheckTime).TotalMinutes >= checkOnPartyTimeGap))
            {
                // set to current time, so that we can check up on the party again after another n number of minutes (checkOnPartyTimeGap)
                lastPartyCheckTime = DateTime.Now;

                // Has anyone left the party
                // open the social menu 
                if (Zeta.Internals.UIElement.IsValidElement(0xAB9216FD8AADCFF9) && (Button = Zeta.Internals.UIElement.FromHash(0xAB9216FD8AADCFF9)) != null)
                {
                    Button.Click();
                }

				// Pause for a bit while checking on the party, as long as we are not in combat
				if (Zeta.CommonBot.CombatTargeting.Instance.FirstNpc == null)
				{
					// pause for a few seconds, while bot checks for missing members
					Log("Okay, time to see if any of these bitches have split!");
					randomTime = (randomNumber.Next(1, 3) * 1000);
					Thread.Sleep(randomTime);
				}
				else					
					Log("Unable to check on party integrity, we are in combat!");
				
                // is there a party invite button in the 1st friend location (2 member party)
                if (Zeta.Internals.UIElement.IsValidElement(0xC590DACA798C3CA4) && (Button = Zeta.Internals.UIElement.FromHash(0xC590DACA798C3CA4)) != null)
                {
                    if (Button.IsVisible && Button.IsEnabled)
                    {
                        Log("Someone has left the group! Time to bail out, dudes!");
                        // YES, therefore 1 of out party members has left
                        partyMemberHasLeft = true;
                    }
                }
                // is there a party invite button in the 2nd friend location (3 member party)
                if (Zeta.Internals.UIElement.IsValidElement(0x270DE7E871AC762B) && (Button = Zeta.Internals.UIElement.FromHash(0x270DE7E871AC762B)) != null)
                {
                    if ((totalNumberOfPartyMembers >= 3) && (Button.IsVisible && Button.IsEnabled))
                    {
                        Log("Someone has left the group! Time to bail out, dudes!");
                        // YES, therefore 1 of out party members has left
                        partyMemberHasLeft = true;
                    }
                }
                // is there a party invite button in the 3rd friend location (4 member party)
                if (Zeta.Internals.UIElement.IsValidElement(0x5E598FAE1E003BBE) && (Button = Zeta.Internals.UIElement.FromHash(0x5E598FAE1E003BBE)) != null)
                {
                    if ((totalNumberOfPartyMembers == 4) && (Button.IsVisible && Button.IsEnabled))
                    {
                        Log("Someone has left the group! Time to bail out, dudes!");
                        // YES, therefore 1 of out party members has left
                        partyMemberHasLeft = true;
                    }
                }

                // Close social menu
                if (Zeta.Internals.UIElement.IsValidElement(0x7B1FD584DA74FA94) && (Button = Zeta.Internals.UIElement.FromHash(0x7B1FD584DA74FA94)) != null)
                {
                    Button.Click();
                }

                // If someone has left the party, we need to create a new game
                if (partyMemberHasLeft == true)
                {
                    // increment the number of games created counter by 1
                    numberOfGameCreationsInTheLastTenMins++;
                    // reset back to false, ready for the next run
                    partyMemberHasLeft = false;
                    // TP back to base				
                    Log("TP'ing back to base");
                    ZetaDia.Actors.Me.UseTownPortal();
                    // Pause for a bit
                    Thread.Sleep(5000);
                    // reload profile				
                    Log("Reloading the profile");
                    Zeta.CommonBot.ProfileManager.Load(GlobalSettings.Instance.LastProfile);
                    Thread.Sleep(1000);
                    // Leave game				
                    Log("Leaving game!");
                    ZetaDia.Service.Games.LeaveGame();
                }

            }
			
			// Follower leaving message
			// need to OKAY this in order to get rid of it
            if (Zeta.Internals.UIElement.IsValidElement(0xF85A00117F5902E9) && (Button = Zeta.Internals.UIElement.FromHash(0xF85A00117F5902E9)) != null)
            {
                if (Button.IsVisible)
                {
					Log("Follower is leaving message has popped up");
					// pause for a few seconds
					Log("Wait for the leader to create a new game");
					randomTime = (randomNumber.Next(1, 3) * 1000);
					Thread.Sleep(randomTime);			
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
					// pause for a few seconds
					Log("Wait for the leader to create a new game");
					randomTime = (randomNumber.Next(1, 3) * 1000);
					Thread.Sleep(randomTime);
					// click on the YES button
					Button.Click();
                }
            }
        } // END OF OnPulse()

        /*
            This is executed whenever a new game is created
			Its main purpose here is to create a party
        */
        public void GameChanged(object sender, EventArgs e)
        {
            Log("New game!");
            // Create a button object to represent the buttons on the screen. This is used for one button at a time.
            Zeta.Internals.UIElement Button = null;

            Log("===================");
            Log("Starting a new game");
            Log("===================");

			Log("************");
            Log("Create party");
			Log("************");
            // pause to wait for other char to catch up
			Log("Wait for the leader to create a new game");
			randomTime = (randomNumber.Next(3, 5) * 1000);
			Thread.Sleep(randomTime);
			
            // open the social menu 
            if (Zeta.Internals.UIElement.IsValidElement(0xAB9216FD8AADCFF9) && (Button = Zeta.Internals.UIElement.FromHash(0xAB9216FD8AADCFF9)) != null)
            {
                Button.Click();
            }
			
			// pause for a bit
			Log("First attempt at creating party");
			randomTime = (randomNumber.Next(1, 2) * 1000);
			Thread.Sleep(randomTime);
			
			// First round of invites
			inviteToTheParty();
			
			// pause for a bit
			Log("Second attempt at creating party");
			randomTime = (randomNumber.Next(1, 3) * 1000);
			Thread.Sleep(randomTime);
			
			// Second round of invites
			inviteToTheParty();
			
			// pause for a bit
			Log("Third attempt at creating party");
			randomTime = (randomNumber.Next(2, 4) * 1000);
			Thread.Sleep(randomTime);
			
			// Third round of invites
			inviteToTheParty();
			
			// pause for a bit
			randomTime = (randomNumber.Next(1, 3) * 1000);
			Thread.Sleep(randomTime);

            // Close social menu
            if (Zeta.Internals.UIElement.IsValidElement(0x7B1FD584DA74FA94) && (Button = Zeta.Internals.UIElement.FromHash(0x7B1FD584DA74FA94)) != null)
            {
                Button.Click();
            }
			
			// pause for a bit
			randomTime = (randomNumber.Next(1, 2) * 1000);
			Thread.Sleep(randomTime);

            Log("Party up, let's go!");
        } // END OF GameChanged(object sender, EventArgs e)
		
		/*
			This method deals with the invites to the party
		 */
        public void inviteToTheParty()
        {
            // Create a button object to represent the buttons on the screen. This is used for one button at a time.
            Zeta.Internals.UIElement Button = null;
			
            // invite 1st char			
            if (totalNumberOfPartyMembers >= 2 && (Zeta.Internals.UIElement.IsValidElement(0xC590DACA798C3CA4) && (Button = Zeta.Internals.UIElement.FromHash(0xC590DACA798C3CA4)) != null))
            {
                if (Button.IsVisible && Button.IsEnabled)
                {
                    Log("Sending party invite for first char");
                    Button.Click();
                }
            }
			
			// pause for a bit
			randomTime = (randomNumber.Next(1, 2) * 1000);
			Thread.Sleep(randomTime);
            // invite 2nd char
            if (totalNumberOfPartyMembers >= 3 && (Zeta.Internals.UIElement.IsValidElement(0x270DE7E871AC762B) && (Button = Zeta.Internals.UIElement.FromHash(0x270DE7E871AC762B)) != null))
            {
                if (Button.IsVisible && Button.IsEnabled)
                {
                    Log("Sending party invite for second char");
                    Button.Click();
                }
            }
			
			// pause for a bit
			randomTime = (randomNumber.Next(1, 2) * 1000);
			Thread.Sleep(randomTime);
            // invite 3rd char
            if (totalNumberOfPartyMembers == 4 && (Zeta.Internals.UIElement.IsValidElement(0x5E598FAE1E003BBE) && (Button = Zeta.Internals.UIElement.FromHash(0x5E598FAE1E003BBE)) != null))
            {
                if (Button.IsVisible && Button.IsEnabled)
                {
                    Log("Sending party invite for third char");
                    Button.Click();
                }
            }
			
			// pause for a bit
			randomTime = (randomNumber.Next(1, 2) * 1000);
			Thread.Sleep(randomTime);
		} // END OF inviteToTheParty()
		
		

        public void GameLeft(object sender, EventArgs e)
        {
            Log("Leaving game!");
        } // END OF GameLeft(object sender, EventArgs e)

        /*
            This method initialises a number of variables to the starting values required when the char starts on a new run after a break or
            when DB is first run
         */
        private void Initialise_All()
        {
            // initialise to current time, so that we can check on the party status every so often (e.g. every 2 mins)
            lastPartyCheckTime = DateTime.Now;
            partyMemberHasLeft = false;
            // 1st game created
            numberOfGameCreationsInTheLastTenMins = 1;
            // set the game creation monitor to the current time
            // we will need to check 10 minutes from now, how many games were created
            gameCreationMonitorStartTime = DateTime.Now;
			// Load the configuration file for the config window
		//	LoadConfigurationFile();
			
            Log("Initialsiation completed!");
        } // END OF Initialise_All()
		
		
		
		
        // ******************************************************
        // *****  Load the Config GUI's configuration file  *****
        // ******************************************************
        private void LoadConfigurationFile()
        {
            // Check that the configuration file exists, if not create one
            if (!File.Exists(@"Plugins\PartyLeader\ConfigSettings"))
            {
                Log("Configuration file does not exist, we are creating a new one based on the default values: ");
				// create a new config file
                SaveConfigurationFile();
                return;
            }
            // Load the config file
            using (StreamReader configReader = new StreamReader(@"Plugins\PartyLeader\ConfigSettings"))
            {
				// read in the first line
				string[] config = configReader.ReadLine().Split('=');
				check2DudePartySetting = Convert.ToBoolean(config[1]);
				// read in the second line
				config = configReader.ReadLine().Split('=');
				check3DudePartySetting = Convert.ToBoolean(config[1]);
				// read in the third line
				config = configReader.ReadLine().Split('=');
				check4DudePartySetting = Convert.ToBoolean(config[1]);
				// read in the fourth line
				config = configReader.ReadLine().Split('=');
				enablePartyCheck = Convert.ToBoolean(config[1]);
				
                configReader.Close();
            }
        } // END OF LoadConfigurationFile()
		
		// ***********************************************
        // ***** Save the Config GUI's Configuration *****
        // ***********************************************
        private void SaveConfigurationFile()
        {
            FileStream configStream = File.Open(@"Plugins\PartyLeader\ConfigSettings", FileMode.Create, FileAccess.Write, FileShare.Read);
            using (StreamWriter configWriter = new StreamWriter(configStream))
            {
                configWriter.WriteLine("check2DudeParty=" + check2DudeParty.IsChecked.ToString());
                configWriter.WriteLine("check3DudeParty=" + check3DudeParty.IsChecked.ToString());
                configWriter.WriteLine("check4DudeParty=" + check4DudeParty.IsChecked.ToString());
                configWriter.WriteLine("enablePartyCheck=" + enablePartyCheck.ToString());
            }
            configStream.Close();
        } // END OF SaveConfiguration()
		
	
        // ********************************************
        // *********** CONFIG WINDOW REGION ***********
        // ********************************************
		// original version by GilesSmith (from GilesCombatReplacer)
		// half inched and reconfigured by ChuckyEgg ;)
        #region configWindow
        public Window DisplayWindow
        {
            get
            {
                if (!File.Exists(@"Plugins\PartyLeader\PartyLeader.xaml"))
                    Log("ERROR: Can't find PartyLeader.xaml");
                try
                {
                    if (configWindow == null)
                    {
                        configWindow = new Window();
                    }
                    StreamReader xamlStream = new StreamReader(@"Plugins\PartyLeader\PartyLeader.xaml");
                    DependencyObject xamlContent = XamlReader.Load(xamlStream.BaseStream) as DependencyObject;
                    configWindow.Content = xamlContent;
					
					// 3 RADIO BUTTONS - check2DudeParty, check3DudeParty, check4DudeParty
                    check2DudeParty = LogicalTreeHelper.FindLogicalNode(xamlContent, "check2DudeParty") as RadioButton;
                    check2DudeParty.Checked += new RoutedEventHandler(check2DudeParty_Checked);
					
                    check3DudeParty = LogicalTreeHelper.FindLogicalNode(xamlContent, "check3DudeParty") as RadioButton;
                    check3DudeParty.Checked += new RoutedEventHandler(check3DudeParty_Checked);
					
                    check4DudeParty = LogicalTreeHelper.FindLogicalNode(xamlContent, "check4DudeParty") as RadioButton;
                    check4DudeParty.Checked += new RoutedEventHandler(check4DudeParty_Checked);
					
					// 2 BUTTONS - btnCheckPartyIntegrity, btnDone
                    btnCheckPartyIntegrity = LogicalTreeHelper.FindLogicalNode(xamlContent, "btnCheckPartyIntegrity") as Button;
                    btnCheckPartyIntegrity.Click += new RoutedEventHandler(btnCheckPartyIntegrity_Click);

                    btnDone = LogicalTreeHelper.FindLogicalNode(xamlContent, "btnDone") as Button;
                    btnDone.Click += new RoutedEventHandler(btnDone_Click);

                    UserControl mainControl = LogicalTreeHelper.FindLogicalNode(xamlContent, "mainControl") as UserControl;
                    // Set height and width to main window
                    configWindow.Height = mainControl.Height + 30;
                    configWindow.Width = mainControl.Width;
                    configWindow.Title = "Party Leader / Party Dude";

                    // On load example
                    configWindow.Loaded += new RoutedEventHandler(configWindow_Loaded);
                    configWindow.Closed += configWindow_Closed;

                    // Add our content to our main window
                    configWindow.Content = xamlContent;
                }
                catch (XamlParseException ex)
                {
                    // You can get specific error information like LineNumber from the exception
                    Log(ex.ToString());
                }
                catch (Exception ex)
                {
                    // Some other error
                    Log(ex.ToString());
                }
                return configWindow;
            }
        }
		
		/*
			This method initialises the controls to their default settings when the config window is displayed
		 */
        private void configWindow_Loaded(object sender, RoutedEventArgs e)
        {
			// Load the configuration file for the config window
			LoadConfigurationFile();
			// default setting for number of people in the party
			check2DudeParty.IsChecked = check2DudePartySetting;
			check3DudeParty.IsChecked = check3DudePartySetting;
			check4DudeParty.IsChecked = check4DudePartySetting;	
			// default settings for the check on the party state (checking if anyone has anyone left the party)
			// set the button's text to YES
			if (enablePartyCheck == true)
			{
				btnCheckPartyIntegrity.Content = "YES";
			}
			else
				btnCheckPartyIntegrity.Content = "NO";
        }
		
		/*
			This method closes the config window
		 */
        private void configWindow_Closed(object sender, EventArgs e)
        {
            configWindow = null;
        }

		/*
			These 3 methods are the events of the radio buttons, and relate to the number of
			people in the party
		 */
        private void check2DudeParty_Checked(object sender, RoutedEventArgs e)
        {
            totalNumberOfPartyMembers = 2;
        }
        private void check3DudeParty_Checked(object sender, RoutedEventArgs e)
        {
            totalNumberOfPartyMembers = 3;
        }
        private void check4DudeParty_Checked(object sender, RoutedEventArgs e)
        {
            totalNumberOfPartyMembers = 4;
        }
		
		/*
			This method represents the btnCheckPartyIntegrity button event when someone left mouse clicks on the button
			btnCheckPartyIntegrity activates and deactivates the checking by the leader of the integrity of the party
			true/YES = leader will check every so often to see if anybody has left the party
			false/NO = leader will not check on the integrity of the party
		 */
        private void btnCheckPartyIntegrity_Click(object sender, RoutedEventArgs e)
        {
			if (enablePartyCheck == true)
			{
				// set is to false
				enablePartyCheck = false;				
				// set the button's text to NO
				btnCheckPartyIntegrity.Content = "NO";
			}
			else // enablePartyCheck is currently set to false
			{
				// set is to true
				enablePartyCheck = true;				
				// set the button's text to YES
				btnCheckPartyIntegrity.Content = "YES";
			}
        }
		
		/*
			This method represents the btnDone button event when someone left mouse clicks on the button
			It closes the configuration window
		 */		
        private void btnDone_Click(object sender, RoutedEventArgs e)
        {
			// Save the current config window settings to the config file
            SaveConfigurationFile();
			// close the config GUI
            configWindow.Close();
        }
		
        #endregion
        // ***************************************************
        // *********** END OF CONFIG WINDOW REGION ***********
        // ***************************************************

    }
	
}

