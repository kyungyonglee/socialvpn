/*
Copyright (C) 2009 Pierre St Juste <ptony82@ufl.edu>, University of Florida

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.IO;

using Brunet;

#if SVPN_NUNIT
using NUnit.Framework;
#endif

namespace SocialVPN {

  /**
   * This class is in charge of making connections between friends. It
   * manages the social networking backends as well as the identity providers
   * of the system.
   */
  public class SocialConnectionManager {

    /**
     * The start time for the timer thread.
     */
    private const int STARTTIME = 30000;

    /**
     * The interval time for the timer thread.
     */
    private const int INTERVALTIME = 300000;

    /**
     * The node which accepts peers based on certificates.
     */
    protected readonly SocialNode _node;

    /*
     * The identity provider.
     */
    protected readonly IProvider _provider;

    /**
     * The social network or relationship provider.
     */
    protected readonly ISocialNetwork _network;

    /**
     * The list of unique friend ids.
     */
    protected readonly List<string> _friends;

    /** 
     * The HTTP interface to manage socialvpn.
     */
    protected readonly HttpInterface _http;

    /**
     * The handles RPC for socialvpn.
     */
    protected readonly SocialRpcHandler _srh;

    /**
     * Timer thread.
     */
    protected readonly Timer _timer_thread;

    /**
     * Constructor.
     * @param node the social node.
     * @param provider the identity provider.
     * @param network the social network.
     * @param port the port number for the HTTP interface.
     * @param srh the social rpc handler.
     */
    public SocialConnectionManager(SocialNode node, IProvider provider,
                                   ISocialNetwork network, string port,
                                   SocialRpcHandler srh) {
      _node = node;
      _provider = provider;
      _network = network;
      _friends = new List<string>();
      _http = new HttpInterface(port);
      _http.ProcessEvent += ProcessHandler;
      _http.Start();
      _srh = srh;
      _srh.SyncEvent += SyncHandler;
      _timer_thread = new Timer(new TimerCallback(TimerHandler), null,
                                STARTTIME, INTERVALTIME);
    }

    /**
     * Timer event handler.
     * @param obj the default object.
     */
    public void TimerHandler(Object obj) {
      try {
        UpdateFriends();
        _node.PublishCertificate();
        _timer_thread.Change(INTERVALTIME, INTERVALTIME);
      } catch (Exception e) {
        _timer_thread.Change(INTERVALTIME, INTERVALTIME);
        ProtocolLog.Write(SocialLog.SVPNLog, e.Message);
        ProtocolLog.Write(SocialLog.SVPNLog, "TIMER HANDLER FAILURE " +
                          DateTime.Now.ToString());
      }
    }

    /**
     * Process event handler.
     * @param obj the default object.
     * @param eargs the event arguments.
     */
    public void ProcessHandler(Object obj, EventArgs eargs) {
      Dictionary<string, string> request = (Dictionary<string,string>) obj;
      if(request.ContainsKey("m")) {
        switch(request["m"]) {
          case "add":
            AddFriends(request["uids"]);;
            break;

          case "addfpr":
            AddFingerprints(request["fprs"]);
            break;
            
          case "allow":
            AllowFriends(request["fprs"]);
            break;

          case "block":
            BlockFriends(request["fprs"]);
            break;

          case "login":
            Login(request["user"], request["pass"]);
            UpdateFriends();
            break;

          default:
            break;
        }
      }
      request["response"] = GetState();
    }

    /**
     * Sync event handler.
     * @param obj the default object.
     * @param eargs the event arguments.
     */
    public void SyncHandler(Object obj, EventArgs eargs) {
      string dht_key = (string) obj;
      string[] parts = dht_key.Split(':');
      string uid = parts[1];

      // Makes sure sync request came from a friend
      if(!_friends.Contains(uid)) {
        UpdateFriendUids();  
      }
      /*
      // Verify fingerprint with the identity provider
      if(_friends.Contains(uid)) {
        List<string> fingerprints = _provider.GetFingerprints(uid);
        if(fingerprints.Contains(dht_key)) {
          _node.AddDhtFriend(dht_key);
        }
      }
      */
      _node.AddDhtFriend(dht_key);
    }

    /**
     * Generates an XML string representing state of the system.
     */
    protected string GetState() {
      SocialState state = new SocialState();
      state.LocalUser = _node.LocalUser;
      state.Friends = new SocialUser[_node.Friends.Count];
      _node.Friends.Values.CopyTo(state.Friends, 0);
      return SocialUtils.ObjectToXml<SocialState>(state);
    }

    /**
     * Updates friends and adds to socialvpn.
     */
    protected void UpdateFriends() {
      UpdateFriendUids();
      foreach(string uid in _friends) {
        AddFriend(uid);
      }
      _provider.StoreFingerprint();
    }

    /**
     * Updates friend uids from social newtork.
     */
    protected void UpdateFriendUids() {
      List<string> new_friends = _network.GetFriends();
      foreach(string uid in new_friends) {
        if(!_friends.Contains(uid)) {
          _friends.Add(uid);
        }
      }
    }

    /**
     * Adds a list of friends seperated by newline.
     * @param friendlist a list of friends unique identifiers.
     */
    protected void AddFriends(string friendlist) {
      string[] friends = friendlist.Split('\n');
      foreach(string friend in friends) {
        AddFriend(friend);
      }
    }

    /**
     * Adds a list of fingerprints seperated by newline.
     * @param fprlist a list of fingerprints.
     */
    protected void AddFingerprints(string fprlist) {
      string[] fprs = fprlist.Split('\n');
      foreach(string fpr in fprs) {
        _node.AddDhtFriend(fpr);
      }
    }

    /**
     * Allow a list of fingerprints seperated by newline.
     * @param fprlist a list of fingerprints.
     */
    protected void AllowFriends(string fprlist) {
      string[] fprs = fprlist.Split('\n');
      foreach(string fpr in fprs) {
        _node.AddFriend(_node.Friends[fpr]);
      }
    }

    /**
     * Block a list of fingerprints seperated by newline.
     * @param fprlist a list of fingerprints.
     */
    protected void BlockFriends(string fprlist) {
      string[] fprs = fprlist.Split('\n');
      foreach(string fpr in fprs) {
        _node.RemoveFriend(_node.Friends[fpr]);
      }
    }

    /**
     * Adds a friend based on user id.
     * @param uid the friend's user id.
     */
    protected void AddFriend(string uid) {
      if(!_friends.Contains(uid)) _friends.Add(uid);
      List<string> fingerprints = _provider.GetFingerprints(uid);
      foreach(string fpr in fingerprints) {
        _node.AddDhtFriend(fpr);
      }
    }

    /**
     * Logins into a identity provider backend.
     * @param username the username.
     * @param password the password.
     */
    protected bool Login(string username, string password) {
      return _provider.Login(username, password);
    }
  }

  /**
   * The interface for an identity provider.
   */
  public interface IProvider {

    bool Login(string username, string password);
    /**
     * Retrieves the fingerprints of a particular peer.
     */
    List<string> GetFingerprints(string uid);

    /**
     * Stores the fingerprint of a peer.
     */
    bool StoreFingerprint();
  }

  /**
   * The interface for a social network.
   */
  public interface ISocialNetwork {
    /**
     * Get a list of friends from the social network.
     */
    List<string> GetFriends();
  }

  /**
   * This class defines the social state of the system.
   */
  public class SocialState {
    /**
     * The local user.
     */
    public SocialUser LocalUser;
    /**
     * The list of friends.
     */
    public SocialUser[] Friends;
  }

#if SVPN_NUNIT
  [TestFixture]
  public class SocialConnectionManagerTester {
    [Test]
    public void SocialConnectionManagerTest() {
      Assert.AreEqual("test", "test");
    }
  } 
#endif
}
