using System.Collections.Generic;
using System.IO;
using Lockstep.Mugen.Battle.Net;
using Lockstep.Network;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.Battle.Net
{
    public sealed class MugenMatchServerCoreTests
    {
        [Test]
        public void TwoPlayersMatchReadyAndRelayInput()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);
            server.OnConnected(10);
            server.OnConnected(11);

            server.HandleMessage(10, Find("A", "kfm,kfm,kfm"));
            server.HandleMessage(11, Find("B", "final,final,final"));

            MatchFoundMsg aFound = sink.Take<MatchFoundMsg>(10);
            MatchFoundMsg bFound = sink.Take<MatchFoundMsg>(11);
            Assert.That(aFound.RoomId, Is.EqualTo(bFound.RoomId));
            Assert.That(aFound.LocalPlayerId, Is.EqualTo(0));
            Assert.That(bFound.LocalPlayerId, Is.EqualTo(1));
            Assert.That(aFound.Team0Csv, Is.EqualTo("kfm,kfm,kfm"));
            Assert.That(aFound.Team1Csv, Is.EqualTo("final,final,final"));

            server.HandleMessage(10, new RoomReadyMsg
            {
                RoomId = aFound.RoomId,
                RoomSeed = aFound.Seed,
                LocalPlayerId = 0,
                PlayerCount = 2,
            });
            Assert.That(sink.TryTake<StartMatchMsg>(10, out _), Is.False);
            server.HandleMessage(11, new RoomReadyMsg
            {
                RoomId = bFound.RoomId,
                RoomSeed = bFound.Seed,
                LocalPlayerId = 1,
                PlayerCount = 2,
            });
            Assert.That(sink.Take<StartMatchMsg>(10).RoomId, Is.EqualTo(aFound.RoomId));
            Assert.That(sink.Take<StartMatchMsg>(11).RoomId, Is.EqualTo(aFound.RoomId));

            server.HandleMessage(10, new MugenInputMsg { Frame = 7, PlayerId = 0, Input = 16 });
            MugenInputMsg relayed = sink.Take<MugenInputMsg>(11);
            Assert.That(relayed.Frame, Is.EqualTo(7));
            Assert.That(relayed.PlayerId, Is.EqualTo(0));
            Assert.That(sink.TryTake<MugenInputMsg>(10, out _), Is.False);
        }

        [Test]
        public void CancelledQueueEntryDoesNotConsumeNextPlayer()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);
            server.OnConnected(1);
            server.OnConnected(2);
            server.OnConnected(3);

            server.HandleMessage(1, Find("Cancel", "a"));
            server.HandleMessage(1, new CancelMatchMsg());
            server.HandleMessage(2, Find("B", "b"));
            Assert.That(server.WaitingCount, Is.EqualTo(1));
            server.HandleMessage(3, Find("C", "c"));

            Assert.That(sink.Take<MatchFoundMsg>(2).LocalPlayerId, Is.EqualTo(0));
            Assert.That(sink.Take<MatchFoundMsg>(3).LocalPlayerId, Is.EqualTo(1));
        }

        [Test]
        public void IncompatibleQueueHeadDoesNotBlockCompatibleLaterPlayers()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);

            server.HandleMessage(1, Find("Old", "a", hash: "old"));
            server.HandleMessage(2, Find("B", "b", hash: "same"));
            server.HandleMessage(3, Find("C", "c", hash: "same"));

            Assert.That(server.WaitingCount, Is.EqualTo(1));
            Assert.That(sink.TryTake<MatchFoundMsg>(1, out _), Is.False);
            Assert.That(sink.Take<MatchFoundMsg>(2).LocalPlayerId, Is.EqualTo(0));
            Assert.That(sink.Take<MatchFoundMsg>(3).LocalPlayerId, Is.EqualTo(1));
        }

        [Test]
        public void NewFindMatchWithSameClientInstanceReplacesOldQueuedConnection()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);

            server.HandleMessage(1, Find("Old", "a", requestId: 1, instanceId: "same-instance"));
            server.HandleMessage(2, Find("New", "a", requestId: 2, instanceId: "same-instance"));

            Assert.That(server.WaitingCount, Is.EqualTo(1));
            RoomClosedMsg oldClosed = sink.Take<RoomClosedMsg>(1);
            Assert.That(oldClosed.Reason, Is.EqualTo("match superseded"));

            server.HandleMessage(3, Find("Opponent", "b", requestId: 3, instanceId: "opponent-instance"));

            Assert.That(sink.TryTake<MatchFoundMsg>(1, out _), Is.False);
            Assert.That(sink.Take<MatchFoundMsg>(2).LocalPlayerId, Is.EqualTo(0));
            Assert.That(sink.Take<MatchFoundMsg>(3).LocalPlayerId, Is.EqualTo(1));
            Assert.That(server.WaitingCount, Is.EqualTo(0));
        }

        [Test]
        public void StaleCancelRequestDoesNotCloseNewQueueOrRoom()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);

            server.HandleMessage(1, Find("A", "a", requestId: 1));
            server.HandleMessage(1, Find("A", "a", requestId: 2));
            server.HandleMessage(1, new CancelMatchMsg { RequestId = 1 });

            Assert.That(server.WaitingCount, Is.EqualTo(1));

            server.HandleMessage(2, Find("B", "b", requestId: 3));
            int roomId = sink.Take<MatchFoundMsg>(1).RoomId;
            sink.Take<MatchFoundMsg>(2);
            server.HandleMessage(1, new CancelMatchMsg { RequestId = 1 });

            Assert.That(server.RoomCount, Is.EqualTo(1));
            Assert.That(sink.TryTake<RoomClosedMsg>(1, out _), Is.False);
            Assert.That(roomId, Is.GreaterThan(0));
        }

        [Test]
        public void StrangerCannotCloseAnotherRoom()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);
            server.OnConnected(1);
            server.OnConnected(2);
            server.OnConnected(3);
            server.HandleMessage(1, Find("A", "a"));
            server.HandleMessage(2, Find("B", "b"));
            int roomId = sink.Take<MatchFoundMsg>(1).RoomId;
            sink.Take<MatchFoundMsg>(2);

            server.HandleMessage(3, new LeaveRoomMsg { RoomId = roomId });
            Assert.That(server.RoomCount, Is.EqualTo(1));
            Assert.That(sink.TryTake<RoomClosedMsg>(1, out _), Is.False);
            Assert.That(sink.TryTake<RoomClosedMsg>(2, out _), Is.False);
        }

        [Test]
        public void CancelAfterRoomFoundClosesRoomForBothPlayers()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);
            server.HandleMessage(1, Find("A", "a"));
            server.HandleMessage(2, Find("B", "b"));
            int roomId = sink.Take<MatchFoundMsg>(1).RoomId;
            sink.Take<MatchFoundMsg>(2);

            server.HandleMessage(1, new CancelMatchMsg());

            Assert.That(server.RoomCount, Is.EqualTo(0));
            Assert.That(sink.Take<RoomClosedMsg>(1).RoomId, Is.EqualTo(roomId));
            Assert.That(sink.Take<RoomClosedMsg>(2).RoomId, Is.EqualTo(roomId));
        }

        [Test]
        public void CompletedLeaveClosesRoomAsMatchCompleted()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);
            int roomId = StartReadyRoom(server, sink);

            server.HandleMessage(1, new LeaveRoomMsg { RoomId = roomId, MatchCompleted = true });

            Assert.That(server.RoomCount, Is.EqualTo(0));
            Assert.That(sink.Take<RoomClosedMsg>(1).Reason, Is.EqualTo("match completed"));
            Assert.That(sink.Take<RoomClosedMsg>(2).Reason, Is.EqualTo("match completed"));
        }

        [Test]
        public void NewFindMatchWhileInRoomSupersedesOldRoomAndQueuesNewRequest()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);
            server.HandleMessage(1, Find("A", "a", requestId: 1));
            server.HandleMessage(2, Find("B", "b", requestId: 2));
            int oldRoomId = sink.Take<MatchFoundMsg>(1).RoomId;
            sink.Take<MatchFoundMsg>(2);

            server.HandleMessage(1, Find("A2", "c", requestId: 3));

            Assert.That(server.RoomCount, Is.EqualTo(0));
            Assert.That(server.WaitingCount, Is.EqualTo(1));
            Assert.That(sink.Take<RoomClosedMsg>(1).Reason, Is.EqualTo("match superseded"));
            RoomClosedMsg closedForOpponent = sink.Take<RoomClosedMsg>(2);
            Assert.That(closedForOpponent.RoomId, Is.EqualTo(oldRoomId));
            Assert.That(closedForOpponent.Reason, Is.EqualTo("match superseded"));

            server.HandleMessage(3, Find("C", "d", requestId: 4));

            Assert.That(sink.Take<MatchFoundMsg>(1).RequestId, Is.EqualTo(3));
            Assert.That(sink.Take<MatchFoundMsg>(3).RequestId, Is.EqualTo(4));
        }

        [Test]
        public void RejectsInvalidTeamBeforeQueueing()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);

            server.HandleMessage(1, new FindMatchMsg
            {
                RequestId = 9,
                Nickname = "A",
                TeamCsv = "single",
                ClientVersion = "test",
                ContentHash = "same",
            });

            Assert.That(server.WaitingCount, Is.EqualTo(0));
            Assert.That(sink.Take<RoomClosedMsg>(1).Reason, Is.EqualTo("invalid team size"));
        }

        [Test]
        public void ReadyMismatchDoesNotStartRoom()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);
            server.HandleMessage(1, Find("A", "a"));
            server.HandleMessage(2, Find("B", "b"));
            MatchFoundMsg found = sink.Take<MatchFoundMsg>(1);
            sink.Take<MatchFoundMsg>(2);

            server.HandleMessage(1, new RoomReadyMsg
            {
                RoomId = found.RoomId,
                RoomSeed = found.Seed + 1,
                LocalPlayerId = 0,
                PlayerCount = 2,
            });

            Assert.That(server.RoomCount, Is.EqualTo(0));
            Assert.That(sink.Take<RoomClosedMsg>(1).Reason, Is.EqualTo("ready mismatch"));
            Assert.That(sink.Take<RoomClosedMsg>(2).Reason, Is.EqualTo("ready mismatch"));
            Assert.That(sink.TryTake<StartMatchMsg>(1, out _), Is.False);
        }

        [Test]
        public void HashMismatchClosesRoomForBothPlayers()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);
            int roomId = StartReadyRoom(server, sink);

            server.HandleMessage(1, new MugenHashReportMsg { RoomId = roomId, Frame = 12, PlayerId = 0, Hash = 100 });
            Assert.That(server.RoomCount, Is.EqualTo(1));

            server.HandleMessage(2, new MugenHashReportMsg { RoomId = roomId, Frame = 12, PlayerId = 1, Hash = 101 });

            Assert.That(server.RoomCount, Is.EqualTo(0));
            Assert.That(sink.Take<RoomClosedMsg>(1).Reason, Is.EqualTo("hash mismatch frame 12"));
            Assert.That(sink.Take<RoomClosedMsg>(2).Reason, Is.EqualTo("hash mismatch frame 12"));
        }

        [Test]
        public void SequentialInputBatchDoesNotTripFrameJumpGuard()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);
            StartReadyRoom(server, sink);

            for (int frame = 0; frame < 40; frame++)
            {
                server.HandleMessage(1, new MugenInputMsg { Frame = frame, PlayerId = 0, Input = 16 });
            }

            Assert.That(server.RoomCount, Is.EqualTo(1));
            Assert.That(sink.TryTake<RoomClosedMsg>(1, out _), Is.False);
            Assert.That(sink.TryTake<RoomClosedMsg>(2, out _), Is.False);
        }

        [Test]
        public void ConflictingDuplicateInputClosesRoom()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);
            StartReadyRoom(server, sink);

            server.HandleMessage(1, new MugenInputMsg { Frame = 0, PlayerId = 0, Input = 16 });
            server.HandleMessage(1, new MugenInputMsg { Frame = 0, PlayerId = 0, Input = 32 });

            Assert.That(server.RoomCount, Is.EqualTo(0));
            Assert.That(sink.Take<RoomClosedMsg>(1).Reason, Is.EqualTo("conflicting input"));
            Assert.That(sink.Take<RoomClosedMsg>(2).Reason, Is.EqualTo("conflicting input"));
        }

        [Test]
        public void InvalidInputBitsCloseRoom()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);
            StartReadyRoom(server, sink);

            server.HandleMessage(1, new MugenInputMsg { Frame = 0, PlayerId = 0, Input = 1 << 20 });

            Assert.That(server.RoomCount, Is.EqualTo(0));
            Assert.That(sink.Take<RoomClosedMsg>(1).Reason, Is.EqualTo("invalid input"));
            Assert.That(sink.Take<RoomClosedMsg>(2).Reason, Is.EqualTo("invalid input"));
        }

        [Test]
        public void NegativeInputFrameClosesRoom()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);
            StartReadyRoom(server, sink);

            server.HandleMessage(1, new MugenInputMsg { Frame = -1, PlayerId = 0, Input = 16 });

            Assert.That(server.RoomCount, Is.EqualTo(0));
            Assert.That(sink.Take<RoomClosedMsg>(1).Reason, Is.EqualTo("invalid input"));
            Assert.That(sink.Take<RoomClosedMsg>(2).Reason, Is.EqualTo("invalid input"));
        }

        [Test]
        public void LargeInputFrameJumpClosesRoom()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);
            StartReadyRoom(server, sink);

            server.HandleMessage(1, new MugenInputMsg { Frame = 0, PlayerId = 0, Input = 16 });
            server.HandleMessage(1, new MugenInputMsg { Frame = 20, PlayerId = 0, Input = 16 });

            Assert.That(server.RoomCount, Is.EqualTo(0));
            Assert.That(sink.Take<RoomClosedMsg>(1).Reason, Is.EqualTo("input frame jump"));
            Assert.That(sink.Take<RoomClosedMsg>(2).Reason, Is.EqualTo("input frame jump"));
        }

        [Test]
        public void SameDuplicateInputIsIgnoredWithoutRelayOrClose()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);
            StartReadyRoom(server, sink);

            server.HandleMessage(1, new MugenInputMsg { Frame = 0, PlayerId = 0, Input = 16 });
            Assert.That(sink.Take<MugenInputMsg>(2).Frame, Is.EqualTo(0));

            server.HandleMessage(1, new MugenInputMsg { Frame = 0, PlayerId = 0, Input = 16 });

            Assert.That(server.RoomCount, Is.EqualTo(1));
            Assert.That(sink.TryTake<MugenInputMsg>(2, out _), Is.False);
            Assert.That(sink.TryTake<RoomClosedMsg>(1, out _), Is.False);
            Assert.That(sink.TryTake<RoomClosedMsg>(2, out _), Is.False);
        }

        [Test]
        public void VeryOldPrunedInputIsIgnoredWithoutClose()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);
            StartReadyRoom(server, sink);

            for (int frame = 0; frame <= 200; frame++)
            {
                server.HandleMessage(1, new MugenInputMsg { Frame = frame, PlayerId = 0, Input = 16 });
            }
            server.HandleMessage(1, new MugenInputMsg { Frame = 0, PlayerId = 0, Input = 32 });

            Assert.That(server.RoomCount, Is.EqualTo(1));
            Assert.That(sink.TryTake<RoomClosedMsg>(1, out _), Is.False);
            Assert.That(sink.TryTake<RoomClosedMsg>(2, out _), Is.False);
        }

        [Test]
        public void RoomTimesOutWhenPlayerStopsSendingMessages()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);
            server.Tick(100);
            server.HandleMessage(1, Find("A", "a"));
            server.HandleMessage(2, Find("B", "b"));
            int roomId = sink.Take<MatchFoundMsg>(1).RoomId;
            sink.Take<MatchFoundMsg>(2);

            server.Tick(30100);
            Assert.That(server.RoomCount, Is.EqualTo(1));

            server.Tick(30101);

            Assert.That(server.RoomCount, Is.EqualTo(0));
            Assert.That(sink.Take<RoomClosedMsg>(1).RoomId, Is.EqualTo(roomId));
            Assert.That(sink.Take<RoomClosedMsg>(2).Reason, Is.EqualTo("connection timeout"));
        }

        [Test]
        public void TimeoutWorksWhenClockStartsAtZero()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);
            server.HandleMessage(1, Find("A", "a"));

            server.Tick(121000);

            Assert.That(server.WaitingCount, Is.EqualTo(0));
            Assert.That(sink.Take<RoomClosedMsg>(1).Reason, Is.EqualTo("match timeout"));
        }

        [Test]
        public void PingReturnsPongWithoutEnteringMatchQueue()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);
            server.Tick(1234);

            server.HandleMessage(7, new PingMsg { Sequence = 42, ClientTimeMs = 1000 });

            PongMsg pong = sink.Take<PongMsg>(7);
            Assert.That(pong.Sequence, Is.EqualTo(42));
            Assert.That(pong.ClientTimeMs, Is.EqualTo(1000));
            Assert.That(pong.ServerTimeMs, Is.EqualTo(1234));
            Assert.That(server.WaitingCount, Is.EqualTo(0));
            Assert.That(server.RoomCount, Is.EqualTo(0));
        }

        [Test]
        public void LoadProgressRelaysOnlyToOpponent()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);
            server.HandleMessage(1, Find("A", "a"));
            server.HandleMessage(2, Find("B", "b"));
            MatchFoundMsg found = sink.Take<MatchFoundMsg>(1);
            sink.Take<MatchFoundMsg>(2);

            server.HandleMessage(1, new LoadProgressMsg
            {
                RoomId = found.RoomId,
                PlayerId = 0,
                ProgressPermille = 1200,
                Ready = true,
            });

            LoadProgressMsg relayed = sink.Take<LoadProgressMsg>(2);
            Assert.That(relayed.RoomId, Is.EqualTo(found.RoomId));
            Assert.That(relayed.PlayerId, Is.EqualTo(0));
            Assert.That(relayed.ProgressPermille, Is.EqualTo(1000));
            Assert.That(relayed.Ready, Is.True);
            Assert.That(sink.TryTake<LoadProgressMsg>(1, out _), Is.False);
        }

        [Test]
        public void NetStatusRelaysOnlyToOpponent()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);
            int roomId = StartReadyRoom(server, sink);

            server.HandleMessage(1, new MugenNetStatusMsg
            {
                RoomId = roomId,
                PlayerId = 0,
                Frame = 123,
                WeakDelayMs = 200,
                LatencyMs = 401,
            });

            MugenNetStatusMsg relayed = sink.Take<MugenNetStatusMsg>(2);
            Assert.That(relayed.RoomId, Is.EqualTo(roomId));
            Assert.That(relayed.PlayerId, Is.EqualTo(0));
            Assert.That(relayed.Frame, Is.EqualTo(123));
            Assert.That(relayed.WeakDelayMs, Is.EqualTo(200));
            Assert.That(relayed.LatencyMs, Is.EqualTo(401));
            Assert.That(sink.TryTake<MugenNetStatusMsg>(1, out _), Is.False);
        }

        [Test]
        public void NetStatusBeforeStartIsCachedAndReplayedOnReady()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);
            server.HandleMessage(1, Find("A", "a"));
            server.HandleMessage(2, Find("B", "b"));
            MatchFoundMsg aFound = sink.Take<MatchFoundMsg>(1);
            MatchFoundMsg bFound = sink.Take<MatchFoundMsg>(2);

            server.HandleMessage(1, Ready(aFound.RoomId, aFound.Seed, 0));
            server.HandleMessage(1, new MugenNetStatusMsg
            {
                RoomId = aFound.RoomId,
                PlayerId = 0,
                Frame = 5,
                WeakDelayMs = 30,
                LatencyMs = 77,
            });

            MugenNetStatusMsg immediate = sink.Take<MugenNetStatusMsg>(2);
            Assert.That(immediate.LatencyMs, Is.EqualTo(77));

            server.HandleMessage(2, Ready(bFound.RoomId, bFound.Seed, 1));

            MugenNetStatusMsg replayed = sink.Take<MugenNetStatusMsg>(2);
            Assert.That(replayed.RoomId, Is.EqualTo(aFound.RoomId));
            Assert.That(replayed.PlayerId, Is.EqualTo(0));
            Assert.That(replayed.Frame, Is.EqualTo(5));
            Assert.That(replayed.WeakDelayMs, Is.EqualTo(30));
            Assert.That(replayed.LatencyMs, Is.EqualTo(77));
        }

        [Test]
        public void Stress_MultipleMatchesInputsAndDisconnectsLeaveNoRoomOrQueueResidue()
        {
            Sink sink = new Sink();
            MugenMatchServerCore server = new MugenMatchServerCore(sink);

            for (int match = 0; match < 20; match++)
            {
                int a = 1000 + match * 2;
                int b = a + 1;
                int requestA = match * 2 + 1;
                int requestB = match * 2 + 2;
                server.HandleMessage(a, Find("A" + match, "a,b,c", requestId: requestA));
                server.HandleMessage(b, Find("B" + match, "d,e,f", requestId: requestB));

                MatchFoundMsg foundA = sink.Take<MatchFoundMsg>(a);
                MatchFoundMsg foundB = sink.Take<MatchFoundMsg>(b);
                Assert.That(foundA.RoomId, Is.EqualTo(foundB.RoomId));
                server.HandleMessage(a, Ready(foundA.RoomId, foundA.Seed, 0));
                server.HandleMessage(b, Ready(foundB.RoomId, foundB.Seed, 1));
                Assert.That(sink.Take<StartMatchMsg>(a).RoomId, Is.EqualTo(foundA.RoomId));
                Assert.That(sink.Take<StartMatchMsg>(b).RoomId, Is.EqualTo(foundA.RoomId));

                for (int frame = 0; frame < 12; frame++)
                {
                    server.HandleMessage(a, new MugenInputMsg { Frame = frame, PlayerId = 0, Input = frame % 2 == 0 ? 16 : 32 });
                    Assert.That(sink.Take<MugenInputMsg>(b).Frame, Is.EqualTo(frame));
                    server.HandleMessage(b, new MugenInputMsg { Frame = frame, PlayerId = 1, Input = frame % 3 == 0 ? 64 : 0 });
                    Assert.That(sink.Take<MugenInputMsg>(a).Frame, Is.EqualTo(frame));
                }

                if (match % 3 == 0)
                {
                    server.HandleMessage(a, new LeaveRoomMsg { RoomId = foundA.RoomId });
                    Assert.That(sink.Take<RoomClosedMsg>(a).Reason, Is.EqualTo("player left room"));
                    Assert.That(sink.Take<RoomClosedMsg>(b).Reason, Is.EqualTo("player left room"));
                }
                else if (match % 3 == 1)
                {
                    server.OnDisconnected(b);
                    Assert.That(sink.Take<RoomClosedMsg>(a).Reason, Is.EqualTo("opponent disconnected"));
                }
                else
                {
                    server.HandleMessage(a, new CancelMatchMsg { RequestId = requestA });
                    Assert.That(sink.Take<RoomClosedMsg>(a).Reason, Is.EqualTo("match cancelled"));
                    Assert.That(sink.Take<RoomClosedMsg>(b).Reason, Is.EqualTo("match cancelled"));
                }

                Assert.That(server.RoomCount, Is.EqualTo(0), "room residue at match " + match);
                Assert.That(server.WaitingCount, Is.EqualTo(0), "queue residue at match " + match);
            }
        }

        [Test]
        public void MatchMessagesRoundTripThroughCodec()
        {
            MatchFoundMsg msg = new MatchFoundMsg
            {
                RoomId = 1001,
                LocalPlayerId = 1,
                PlayerCount = 2,
                Seed = 99,
                Team0Csv = "a,b,c",
                Team1Csv = "d,e,f",
                OpponentName = "op",
                RequestId = 77,
            };

            byte[] bytes = MessageCodec.Encode(msg);
            MatchFoundMsg back = (MatchFoundMsg)MessageCodec.Decode(bytes, 0, bytes.Length);
            Assert.That(back.RoomId, Is.EqualTo(msg.RoomId));
            Assert.That(back.RequestId, Is.EqualTo(msg.RequestId));
            Assert.That(back.LocalPlayerId, Is.EqualTo(msg.LocalPlayerId));
            Assert.That(back.Team0Csv, Is.EqualTo(msg.Team0Csv));
            Assert.That(back.Team1Csv, Is.EqualTo(msg.Team1Csv));
            Assert.That(back.OpponentName, Is.EqualTo(msg.OpponentName));

            CancelMatchMsg cancel = new CancelMatchMsg { RequestId = 88 };
            byte[] cancelBytes = MessageCodec.Encode(cancel);
            CancelMatchMsg cancelBack = (CancelMatchMsg)MessageCodec.Decode(cancelBytes, 0, cancelBytes.Length);
            Assert.That(cancelBack.RequestId, Is.EqualTo(cancel.RequestId));

            LeaveRoomMsg leave = new LeaveRoomMsg { RoomId = 12, MatchCompleted = true };
            byte[] leaveBytes = MessageCodec.Encode(leave);
            LeaveRoomMsg leaveBack = (LeaveRoomMsg)MessageCodec.Decode(leaveBytes, 0, leaveBytes.Length);
            Assert.That(leaveBack.RoomId, Is.EqualTo(leave.RoomId));
            Assert.That(leaveBack.MatchCompleted, Is.True);

            RoomClosedMsg closed = new RoomClosedMsg { RequestId = 89, RoomId = 12, Reason = "done" };
            byte[] closedBytes = MessageCodec.Encode(closed);
            RoomClosedMsg closedBack = (RoomClosedMsg)MessageCodec.Decode(closedBytes, 0, closedBytes.Length);
            Assert.That(closedBack.RequestId, Is.EqualTo(closed.RequestId));
            Assert.That(closedBack.RoomId, Is.EqualTo(closed.RoomId));
            Assert.That(closedBack.Reason, Is.EqualTo(closed.Reason));

            MugenHashReportMsg hash = new MugenHashReportMsg { RoomId = 99, Frame = 123, PlayerId = 1, Hash = 0x1122334455667788UL };
            byte[] hashBytes = MessageCodec.Encode(hash);
            MugenHashReportMsg hashBack = (MugenHashReportMsg)MessageCodec.Decode(hashBytes, 0, hashBytes.Length);
            Assert.That(hashBack.RoomId, Is.EqualTo(hash.RoomId));
            Assert.That(hashBack.Frame, Is.EqualTo(hash.Frame));
            Assert.That(hashBack.PlayerId, Is.EqualTo(hash.PlayerId));
            Assert.That(hashBack.Hash, Is.EqualTo(hash.Hash));

            MugenNetStatusMsg status = new MugenNetStatusMsg
            {
                RoomId = 99,
                PlayerId = 1,
                Frame = 321,
                WeakDelayMs = 200,
                LatencyMs = 403,
            };
            byte[] statusBytes = MessageCodec.Encode(status);
            MugenNetStatusMsg statusBack = (MugenNetStatusMsg)MessageCodec.Decode(statusBytes, 0, statusBytes.Length);
            Assert.That(statusBack.RoomId, Is.EqualTo(status.RoomId));
            Assert.That(statusBack.PlayerId, Is.EqualTo(status.PlayerId));
            Assert.That(statusBack.Frame, Is.EqualTo(status.Frame));
            Assert.That(statusBack.WeakDelayMs, Is.EqualTo(status.WeakDelayMs));
            Assert.That(statusBack.LatencyMs, Is.EqualTo(status.LatencyMs));

            PingMsg ping = new PingMsg { Sequence = 12, ClientTimeMs = 345 };
            byte[] pingBytes = MessageCodec.Encode(ping);
            PingMsg pingBack = (PingMsg)MessageCodec.Decode(pingBytes, 0, pingBytes.Length);
            Assert.That(pingBack.Sequence, Is.EqualTo(ping.Sequence));
            Assert.That(pingBack.ClientTimeMs, Is.EqualTo(ping.ClientTimeMs));

            PongMsg pong = new PongMsg { Sequence = 12, ClientTimeMs = 345, ServerTimeMs = 678 };
            byte[] pongBytes = MessageCodec.Encode(pong);
            PongMsg pongBack = (PongMsg)MessageCodec.Decode(pongBytes, 0, pongBytes.Length);
            Assert.That(pongBack.Sequence, Is.EqualTo(pong.Sequence));
            Assert.That(pongBack.ClientTimeMs, Is.EqualTo(pong.ClientTimeMs));
            Assert.That(pongBack.ServerTimeMs, Is.EqualTo(pong.ServerTimeMs));

            LoadProgressMsg load = new LoadProgressMsg { RoomId = 55, PlayerId = 1, ProgressPermille = 890, Ready = false };
            byte[] loadBytes = MessageCodec.Encode(load);
            LoadProgressMsg loadBack = (LoadProgressMsg)MessageCodec.Decode(loadBytes, 0, loadBytes.Length);
            Assert.That(loadBack.RoomId, Is.EqualTo(load.RoomId));
            Assert.That(loadBack.PlayerId, Is.EqualTo(load.PlayerId));
            Assert.That(loadBack.ProgressPermille, Is.EqualTo(load.ProgressPermille));
            Assert.That(loadBack.Ready, Is.EqualTo(load.Ready));

            ServerLogMsg log = new ServerLogMsg { ServerTimeMs = 12345, Message = "queue=1 rooms=0" };
            byte[] logBytes = MessageCodec.Encode(log);
            ServerLogMsg logBack = (ServerLogMsg)MessageCodec.Decode(logBytes, 0, logBytes.Length);
            Assert.That(logBack.ServerTimeMs, Is.EqualTo(log.ServerTimeMs));
            Assert.That(logBack.Message, Is.EqualTo(log.Message));

            FindMatchMsg find = new FindMatchMsg
            {
                RequestId = 90,
                Nickname = "P",
                TeamCsv = "a,b,c",
                ContentHash = "hash",
                ClientVersion = "version",
                ClientInstanceId = "run-1:P",
                ClientBuildVersion = "1.2.3",
                ClientBuildGuid = "build-guid",
                ClientPlatform = "Android",
                ClientDeviceModel = "Pixel",
                ClientDeviceType = "Handheld",
                ClientOperatingSystem = "Android 15",
            };
            byte[] findBytes = MessageCodec.Encode(find);
            FindMatchMsg findBack = (FindMatchMsg)MessageCodec.Decode(findBytes, 0, findBytes.Length);
            Assert.That(findBack.RequestId, Is.EqualTo(find.RequestId));
            Assert.That(findBack.Nickname, Is.EqualTo(find.Nickname));
            Assert.That(findBack.TeamCsv, Is.EqualTo(find.TeamCsv));
            Assert.That(findBack.ContentHash, Is.EqualTo(find.ContentHash));
            Assert.That(findBack.ClientVersion, Is.EqualTo(find.ClientVersion));
            Assert.That(findBack.ClientInstanceId, Is.EqualTo(find.ClientInstanceId));
            Assert.That(findBack.ClientBuildVersion, Is.EqualTo(find.ClientBuildVersion));
            Assert.That(findBack.ClientBuildGuid, Is.EqualTo(find.ClientBuildGuid));
            Assert.That(findBack.ClientPlatform, Is.EqualTo(find.ClientPlatform));
            Assert.That(findBack.ClientDeviceModel, Is.EqualTo(find.ClientDeviceModel));
            Assert.That(findBack.ClientDeviceType, Is.EqualTo(find.ClientDeviceType));
            Assert.That(findBack.ClientOperatingSystem, Is.EqualTo(find.ClientOperatingSystem));
        }

        [Test]
        public void FindMatchMsg_LegacyPayloadWithoutDeviceInfo_DecodesWithEmptyDeviceFields()
        {
            using MemoryStream stream = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write((byte)MsgType.FindMatch);
                writer.Write(7);
                writer.Write("Legacy");
                writer.Write("a,b,c");
                writer.Write("hash");
                writer.Write("version");
                writer.Write("legacy-instance");
            }

            FindMatchMsg decoded = (FindMatchMsg)MessageCodec.Decode(stream.ToArray(), 0, (int)stream.Length);
            Assert.That(decoded.ClientInstanceId, Is.EqualTo("legacy-instance"));
            Assert.That(decoded.ClientBuildVersion, Is.EqualTo(string.Empty));
            Assert.That(decoded.ClientBuildGuid, Is.EqualTo(string.Empty));
            Assert.That(decoded.ClientPlatform, Is.EqualTo(string.Empty));
            Assert.That(decoded.ClientDeviceModel, Is.EqualTo(string.Empty));
            Assert.That(decoded.ClientDeviceType, Is.EqualTo(string.Empty));
            Assert.That(decoded.ClientOperatingSystem, Is.EqualTo(string.Empty));
        }

        static int StartReadyRoom(MugenMatchServerCore server, Sink sink)
        {
            server.HandleMessage(1, Find("A", "a"));
            server.HandleMessage(2, Find("B", "b"));
            MatchFoundMsg aFound = sink.Take<MatchFoundMsg>(1);
            MatchFoundMsg bFound = sink.Take<MatchFoundMsg>(2);

            server.HandleMessage(1, new RoomReadyMsg
            {
                RoomId = aFound.RoomId,
                RoomSeed = aFound.Seed,
                LocalPlayerId = 0,
                PlayerCount = 2,
            });
            server.HandleMessage(2, new RoomReadyMsg
            {
                RoomId = bFound.RoomId,
                RoomSeed = bFound.Seed,
                LocalPlayerId = 1,
                PlayerCount = 2,
            });
            sink.Take<StartMatchMsg>(1);
            sink.Take<StartMatchMsg>(2);
            return aFound.RoomId;
        }

        static RoomReadyMsg Ready(int roomId, int seed, int playerId)
        {
            return new RoomReadyMsg
            {
                RoomId = roomId,
                RoomSeed = seed,
                LocalPlayerId = playerId,
                PlayerCount = 2,
            };
        }

        static FindMatchMsg Find(string name, string team, string hash = "same", int requestId = 0, string instanceId = "")
        {
            if (!team.Contains(","))
            {
                team = team + "," + team + "," + team;
            }

            return new FindMatchMsg
            {
                RequestId = requestId == 0 ? name.Length : requestId,
                Nickname = name,
                TeamCsv = team,
                ClientVersion = "test",
                ContentHash = hash,
                ClientInstanceId = instanceId,
            };
        }

        sealed class Sink : IMugenMatchServerSink
        {
            readonly Dictionary<int, Queue<IMessage>> _messages = new Dictionary<int, Queue<IMessage>>();

            public void Send(int connectionId, IMessage message)
            {
                if (!_messages.TryGetValue(connectionId, out Queue<IMessage> queue))
                {
                    queue = new Queue<IMessage>();
                    _messages.Add(connectionId, queue);
                }
                queue.Enqueue(message);
            }

            public T Take<T>(int connectionId) where T : class, IMessage
            {
                Assert.That(TryTake(connectionId, out T message), Is.True);
                return message;
            }

            public bool TryTake<T>(int connectionId, out T message) where T : class, IMessage
            {
                message = null;
                if (!_messages.TryGetValue(connectionId, out Queue<IMessage> queue))
                {
                    return false;
                }

                int count = queue.Count;
                for (int i = 0; i < count; i++)
                {
                    IMessage value = queue.Dequeue();
                    if (value is T typed)
                    {
                        message = typed;
                        return true;
                    }
                    queue.Enqueue(value);
                }
                return false;
            }
        }
    }
}
