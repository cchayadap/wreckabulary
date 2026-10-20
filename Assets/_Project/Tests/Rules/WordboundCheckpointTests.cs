using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
namespace Wreckabulary.Tests {
 public sealed class WordboundCheckpointTests {
  const string Player="11111111-1111-4111-a111-111111111111", Other="22222222-2222-4222-a222-222222222222", Match="33333333-3333-4333-a333-333333333333";
  const long Now=1700000000000;
  sealed class Http : IOnlineHttpTransport { public Func<OnlineRequest,Task<OnlineResponse>> Reply; public Task<OnlineResponse> Send(OnlineRequest r,CancellationToken ct)=>Reply(r); }
  sealed class Fixture {
   public readonly Http Transport=new Http(); public readonly OnlineCoordinatorClient Client; public readonly OnlineCheckpointService Service;
   public readonly string Identity=new string('a',64); public readonly Dictionary<string,object> Writer;
   public readonly Dictionary<int,string> Chunks=new Dictionary<int,string>(); public string Manifest,Head;
   public Func<OnlineRequest,Task<OnlineResponse>> Override;
   public Fixture() {
    Writer=new Dictionary<string,object>{{"playerId",Player},{"claimId",Other},{"term","1"},{"epoch","0"},{"expiresAt",Now+15000},{"baseHeadId",null},{"admissionOpen",false}};
    Transport.Reply=r=>Override==null?Task.FromResult(Reply(r)):Override(r);
    Client=new OnlineCoordinatorClient(Transport); Client.CreateGuest("Test").GetAwaiter().GetResult();
    var settings=new OnlineSettings("Freestyle","Solo",false,"asia","wordbound-v2","test");
    var content=new OnlineContent("Workshop","proof",new string('b',64),new string('c',64),new string('d',64),new string('e',64));
    Service=new OnlineCheckpointService(Client,new OnlineMatchExpectation(Match,"1",new string('f',64),settings,content),Identity);
   }
   public OnlineResponse Reply(OnlineRequest r) {
    if(r.Path=="/v1/guests")return Json(new Dictionary<string,object>{{"playerId",Player},{"accessToken","test.access.token"},{"refreshToken",Player+"."+OnlineJson.Base64Url(new byte[32])},{"expiresIn",900}});
    var body=OnlineJson.Parse(r.JsonBody);
    if(r.Path.EndsWith("checkpoint-claim")){Writer["claimId"]=body["claimId"].String();return State();}
    if(r.Path.EndsWith("checkpoint-renew")){Writer["expiresAt"]=(long)Writer["expiresAt"]+5000;return State();}
    if(r.Path.EndsWith("checkpoint-open")){Writer["admissionOpen"]=true;return State();}
    if(r.Path.EndsWith("checkpoint-head"))return State();
    if(r.Path.EndsWith("checkpoint-begin")){Manifest=r.JsonBody;return new OnlineResponse(200,"{\"manifest\":"+Manifest+"}");}
    if(r.Path.EndsWith("checkpoint-chunk")){int i=(int)body["index"].Number();Chunks[i]=body["data"].String();return Json(new Dictionary<string,object>{{"uploadId",body["uploadId"].String()},{"index",i}});}
    if(r.Path.EndsWith("checkpoint-finalize")){Head=Manifest;return new OnlineResponse(200,"{\"head\":"+Head+"}");}
    if(r.Path.EndsWith("checkpoint-read")){int i=(int)body["index"].Number();return Json(new Dictionary<string,object>{{"uploadId",body["uploadId"].String()},{"index",i},{"data",Chunks[i]}});}
    throw new InvalidOperationException(r.Path);
   }
   public OnlineResponse State()=>new OnlineResponse(200,"{\"writer\":"+OnlineJson.Encode(Writer)+",\"head\":"+(Head??"null")+",\"previous\":null}");
   public OnlineCheckpointWriter Claim()=>Service.Claim(Guid.NewGuid().ToString()).GetAwaiter().GetResult().Writer;
   public OnlineCheckpointUpload Prepare(string json="{}")=>Service.Prepare(Claim(),null,new OnlineSnapshotMark(Match,Identity,0,1,5,new string('b',64)),json);
   public static OnlineResponse Json(object value)=>new OnlineResponse(200,OnlineJson.Encode(value));
  }
  [Test] public void FullClientRoundTripPinsExactImmutableManifestAndPacket() {
   var f=new Fixture(); var upload=f.Prepare("{\"data\":\"real packet\"}");
   var committed=f.Service.Commit(upload).GetAwaiter().GetResult(); Assert.IsTrue(upload.Manifest.Same(committed));
   Assert.AreEqual("{\"data\":\"real packet\"}",f.Service.Download(committed).GetAwaiter().GetResult());
   Assert.IsTrue(f.Service.Head().GetAwaiter().GetResult().Head.Same(committed));
   var repeated=f.Service.Commit(upload).GetAwaiter().GetResult(); Assert.IsTrue(repeated.Same(committed));
  }
  [Test] public void ManifestHandleCommitsEveryFieldAndStablePreparationIsIdempotent() {
   var f=new Fixture();var writer=f.Claim();var mark=new OnlineSnapshotMark(Match,f.Identity,0,1,5,new string('b',64));
   var a=f.Service.Prepare(writer,null,mark,"{}");var b=f.Service.Prepare(writer,null,mark,"{}");
   Assert.AreEqual(a.Manifest.UploadId,b.Manifest.UploadId);
   Assert.IsFalse(a.Manifest.UploadId==f.Service.Prepare(writer,null,mark,"{\"a\":1}").Manifest.UploadId);
   f.Override=r=>Task.FromResult(new OnlineResponse(200,"{\"manifest\":"+r.JsonBody.Replace("\"totalBytes\":"+a.Manifest.TotalBytes,"\"totalBytes\":"+(a.Manifest.TotalBytes+1))+"}"));
   Assert.Throws<FormatException>(()=>f.Service.Commit(a).GetAwaiter().GetResult());
  }
  [Test] public void ClaimCannotTrustAnotherActorOrUnrestoredHead() {
   var f=new Fixture(); f.Writer["playerId"]=Other;
   Assert.Throws<FormatException>(()=>f.Claim());
   f.Writer["playerId"]=Player;f.Writer["epoch"]="1";
   Assert.Throws<FormatException>(()=>f.Claim());
   f.Writer["epoch"]="0";f.Writer["baseHeadId"]=new string('a',64);
   Assert.Throws<FormatException>(()=>f.Claim());
  }
  [Test] public void RenewDoesNotAcceptNewTermAndOpenRequiresExplicitPhase() {
   var f=new Fixture();var writer=f.Claim();
   Assert.IsFalse(writer.AdmissionOpen);Assert.IsTrue(f.Service.OpenAdmission(writer).GetAwaiter().GetResult().Writer.AdmissionOpen);
   Assert.Greater(f.Service.Renew(writer).GetAwaiter().GetResult().Writer.ExpiresAtMilliseconds,writer.ExpiresAtMilliseconds);
   f.Writer["term"]="2";
   Assert.Throws<FormatException>(()=>f.Service.Renew(writer).GetAwaiter().GetResult());
  }
  [Test] public void LostFinalizeResponseCanReplayTheExactPreparedUpload() {
   var f=new Fixture();var upload=f.Prepare();bool lose=true;
   f.Override=r=>{var response=f.Reply(r);if(r.Path.EndsWith("checkpoint-finalize")&&lose){lose=false;throw new IOException("Simulated lost response");}return Task.FromResult(response);};
   Assert.Throws<IOException>(()=>f.Service.Commit(upload).GetAwaiter().GetResult());
   Assert.IsTrue(f.Service.Head().GetAwaiter().GetResult().Head.Same(upload.Manifest));
   Assert.IsTrue(f.Service.Commit(upload).GetAwaiter().GetResult().Same(upload.Manifest));
  }
  [Test] public void DownloadRejectsWrongIndexAndCorruptBytes() {
   var f=new Fixture();var upload=f.Prepare();var head=f.Service.Commit(upload).GetAwaiter().GetResult();
   f.Override=r=>Task.FromResult(new OnlineResponse(200,f.Reply(r).Json.Replace("\"index\":0","\"index\":1")));
   Assert.Throws<FormatException>(()=>f.Service.Download(head).GetAwaiter().GetResult());
   f.Override=null;var bytes=OnlineJson.DecodeBase64Url(f.Chunks[0],4096);bytes[bytes.Length-1]^=1;f.Chunks[0]=OnlineJson.Base64Url(bytes);
   Assert.Throws<FormatException>(()=>f.Service.Download(head).GetAwaiter().GetResult());
  }
  [Test] public void ChunkRequestsAreBoundedToFourConcurrentHttpOperations() {
   var f=new Fixture();var random=new byte[30000];new Random(2718).NextBytes(random);var upload=f.Prepare(Convert.ToBase64String(random));
   var release=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);int active=0,peak=0;var sync=new object();
   f.Override=async r=>{if(!r.Path.EndsWith("checkpoint-chunk"))return f.Reply(r);lock(sync){active++;peak=Math.Max(peak,active);}await release.Task.ConfigureAwait(false);OnlineResponse response;lock(sync){response=f.Reply(r);active--;}return response;};
   var pending=f.Service.Commit(upload);Assert.AreEqual(4,active);Assert.IsFalse(pending.IsCompleted);release.SetResult(true);
   Assert.IsTrue(pending.GetAwaiter().GetResult().Same(upload.Manifest));Assert.AreEqual(4,peak);Assert.AreEqual(0,active);
  }
 }
}
