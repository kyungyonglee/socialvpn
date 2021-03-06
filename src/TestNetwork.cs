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
using System.Text;

using Brunet;
using Brunet.DistributedServices;

#if SVPN_NUNIT
using NUnit.Framework;
#endif

namespace SocialVPN {

  public class TestNetwork : IProvider, ISocialNetwork {

    public const char DELIM = ',';

    protected readonly string _url;

    protected readonly SocialUser _local_user;

    protected readonly List<string> _fingerprints;

    public TestNetwork(SocialUser user, byte[] certData) {
      _local_user = user;
      _fingerprints = new List<string>();
      _url = "http://socialvpntest.appspot.com/api/";
    }

    public bool Login(string id, string username, string password) {
      return true;
    }

    public bool Logout() {
      return true;
    }

    public List<string> GetFriends() {
      List<string> new_friends = new List<string>();
      Dictionary<string, string> parameters = 
        new Dictionary<string, string>();

      parameters["m"] = "getfriends";
      parameters["uid"] = _local_user.Uid;
      string response = SocialUtils.Request(_url, parameters);

      string[] friends = response.Split(DELIM);
      foreach(string friend in friends) {
        new_friends.Add(friend);
      }
      return new_friends;
    }

    public List<string> GetFingerprints(string[] uids) {
      StringBuilder friendlist = new StringBuilder();
      if(uids != null) {
        for(int i=0; i < uids.Length; i++) {
          if(i < uids.Length - 1) {
            friendlist.Append(uids[i] + DELIM);
          }
          else {
            friendlist.Append(uids[i]);
          }
        }
      }

      List<string> fingerprints = new List<string>();
      Dictionary<string, string> parameters = 
        new Dictionary<string, string>();

      parameters["m"] = "getfprs";
      parameters["uids"] = friendlist.ToString();
      string response = SocialUtils.Request(_url, parameters);

      string[] fprs = response.Split(DELIM);
      foreach(string fpr in fprs) {
        fingerprints.Add(fpr);
      }
      return fingerprints;
    }

    public List<byte[]> GetCertificates(string[] uids) {
      return null;
    }

    public bool StoreFingerprint() {
      List<string> fingerprints = GetFingerprints(new string[] 
                                                  {_local_user.Uid});
      if(!fingerprints.Contains(_local_user.DhtKey)) {
        Dictionary<string, string> parameters = 
          new Dictionary<string, string>();

        parameters["m"] = "store";
        parameters["uid"] = _local_user.Uid;
        parameters["fpr"] = _local_user.DhtKey;
        SocialUtils.Request(_url, parameters);
      }
      return true;
    }

    public bool ValidateCertificate(SocialUser user, byte[] certData) {
      return true;
    }
  }

#if SVPN_NUNIT
  [TestFixture]
  public class TestNetworkTester {
    [Test]
    public void TestNetworkTest() {
      ///*
      string uid = "ptony82@ufl.edu";
      string name = "Pierre St Juste";
      string pcid = "pdesktop";
      string version = "SVPN_0.3.0";
      string country = "US";
      string address = 
        Brunet.Applications.Utils.GenerateAHAddress().ToString();
      SocialUtils.CreateCertificate(uid, name, pcid, version, country,
                                    address, "certificates", "private_key");
      //*/
      string cert_path = System.IO.Path.Combine("certificates", "local.cert");
      byte[] cert_data = SocialUtils.ReadFileBytes(cert_path);
      SocialUser user = new SocialUser(cert_data);

      Console.WriteLine(user);
      TestNetwork backend = new TestNetwork(user, cert_data);
      //backend.StoreFingerprint();
      string[] friends = backend.GetFriends().ToArray();
      foreach(string friend in friends) {
        Console.WriteLine(friend);
      }
      string[] fprs = backend.GetFingerprints(friends).ToArray();
      foreach(string fpr in fprs) {
        Console.WriteLine(fpr);
      }
    }
  } 
#endif

}
