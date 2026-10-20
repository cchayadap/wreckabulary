using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
sealed class Export {
 sealed class World : IWordboundWorldQuery {
  public bool HasLineOfSight(string id, WorldVector from, WorldVector to)=>false;
  public bool CanDeploy(string id, string recipe, WorldVector position)=>false;
  public bool PuzzleSatisfied(string id, ResolvedCoursePuzzle puzzle, WorldBodyState item)=>false;
 }
 static void Main(string[] args) {
  string project=args[0], target=args[1];
  var mode=ModeDefinition.For("Freestyle"); var geometry=CourseTemplates.ProofCatalogue();
  var course=CourseResolver.Resolve(CourseTemplates.FreestyleWorld(),geometry,new CourseResolveRequest("0123456789abcdef",MatchStateValidation.Requirements(mode))).Manifest;
  var items=ItemCatalogue.FromJson(File.ReadAllText(Path.Combine(project,"Assets/_Project/Data/Config/items.json")));
  var rules=mode.CreateGameplayRules(RuleBook.FromJson(File.ReadAllText(Path.Combine(project,"Assets/_Project/Data/Config/rules.json"))).For("Dibs"));
  var binding=new MatchBinding("11111111-1111-4111-a111-111111111111",MatchSnapshotCodec.ProtocolVersion,mode.Hash,course.SourceHash,course.Hash,new string('e',64),new string('f',64),new[]{new WordboundSeat("22222222-2222-4222-a222-222222222222",0,0),new WordboundSeat("33333333-3333-4333-a333-333333333333",0,1)});
  var match=new AuthoritativeMatch(binding,mode,course,geometry,items,rules,new World()); var s=match.Capture(1);
  var actors=s.Actors.Select(a=>new MatchActorState(a.PlayerId,a.Health,a.Stamina,appearance:"coat\nnavy\t\b\u0001 🧸")).ToArray();
  var custom=new MatchSnapshot(s.Binding,s.Epoch,s.Sequence,s.NextWorldEntityId,s.Progress,s.Economy,s.Commands,actors,s.Bodies,s.Resources,s.Mechanisms,s.RandomStreams,s.NarrativeFlags);
  string json=MatchSnapshotCodec.Export(custom);
  var imported=MatchSnapshotCodec.Import(json,binding,mode,course,items,rules); if(!imported.Success)throw new Exception(imported.Error);
  var packet=OnlineSnapshotPacket.Compress(json);
  var data=new Dictionary<string,object>{{"json",json},{"identityHash",binding.Hash},{"wireHash",packet.WireHash},{"rawLength",packet.RawLength},{"totalBytes",packet.Length},{"chunks",Enumerable.Range(0,packet.ChunkCount).Select(i=>(object)OnlineJson.Base64Url(packet.Chunk(i))).ToArray()}};
  File.WriteAllText(target,"export const schema2Fixture = "+OnlineJson.Encode(data)+";\n");
  Console.WriteLine("REAL_SCHEMA2_CSHARP_EXPORT_AND_IMPORT_PASS bytes="+packet.RawLength+" chunks="+packet.ChunkCount);
 }
}
