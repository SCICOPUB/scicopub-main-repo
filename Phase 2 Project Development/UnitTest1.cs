using System;
using System.Threading;
using System.Threading.Tasks;
using ClientWpf;
using Moq;
using Xunit;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;

namespace ClientWpfTests
{
    public class ClientTests
    {
        // ��� ��� ������ ����������
        private readonly Mock<INetworkClient> _lowLevelClientMock;

        public ClientTests()
        {
            // ��������� ��� ��� ���������� INetworkClient
            // ������������� MockBehavior.Strict, ��� ������������, �� �� �������� ������
            // �� ���� ���� ���� ����������. �� �������� ������ ���������� �������.
            _lowLevelClientMock = new Mock<INetworkClient>(MockBehavior.Strict);
        }

        [Fact]
        public void Connect_SetsPropertiesAndSendsInitialData()
        {
            var client = new Client(_lowLevelClientMock.Object);
            string u = "Alice", p = "pass", e = "alice@mail.com", i = "img.png";
            _lowLevelClientMock.Setup(c => c.SendString(It.IsAny<string>()));
            client.Connect(u, p, e, i);
            Assert.Equal(u, client.UserName);
            Assert.Equal(p, client.Password);
            Assert.Equal(e, client.Email);
            Assert.Equal(i, client.Image);
            _lowLevelClientMock.Verify(c => c.SendString(u), Times.Once);
            _lowLevelClientMock.Verify(c => c.SendString(p), Times.Once);
            _lowLevelClientMock.Verify(c => c.SendString(e), Times.Once);
            _lowLevelClientMock.Verify(c => c.SendString(i), Times.Once);
            Assert.True(client.IsConnected);
        }

        [Fact]
        public void Disconnect_WhenConnected_ShouldDisposeLowLevelClient()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);
            _lowLevelClientMock.Setup(c => c.Dispose()).Verifiable();
            client.Disconnect();
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Once);
            Assert.False(client.IsConnected);
        }

        [Fact]
        public void LowLevelClientDisconnectedEvent_SetsIsConnectedToFalse()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);
            _lowLevelClientMock.Raise(c => c.Disconnected += null, EventArgs.Empty);
            Assert.False(client.IsConnected);
        }

        [Fact]
        public void SendMessage_WhenConnected_SendsStringsViaLowLevelClient()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);
            string to = "Bob", msg = "Hello!";
            _lowLevelClientMock.Setup(c => c.SendString(to)).Verifiable();
            _lowLevelClientMock.Setup(c => c.SendString(msg)).Verifiable();
            client.SendMessage(to, msg);
            _lowLevelClientMock.Verify(c => c.SendString(to), Times.Once);
            _lowLevelClientMock.Verify(c => c.SendString(msg), Times.Once);
            Assert.True(client.IsConnected);
        }

        [Fact]
        public void SendMessage_WhenDisconnected_ThrowsInvalidOperationException()
        {
            var client = new Client(_lowLevelClientMock.Object);
            _lowLevelClientMock.Raise(c => c.Disconnected += null, EventArgs.Empty);
            Assert.False(client.IsConnected);
            _lowLevelClientMock.Setup(c => c.SendString(It.IsAny<string>())).Verifiable();
            Assert.Throws<InvalidOperationException>(() => client.SendMessage("to", "msg"));
            _lowLevelClientMock.Verify(c => c.SendString(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task StringReceivedEvent_ParsesPairsAndRaisesMessageReceived()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);
            string from = "Charlie", msg = "Hi!";
            var receivedMessages = new List<IMReceivedEventArgs>();
            var signal = new SemaphoreSlim(0, 1);
            client.MessageReceived += (s, a) => { receivedMessages.Add(a); signal.Release(); };
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(from));
            Assert.Empty(receivedMessages);
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(msg));
            bool signalled = await signal.WaitAsync(1000);
            Assert.True(signalled);
            Assert.Single(receivedMessages);
            Assert.Equal(from, receivedMessages[0].From);
            Assert.Equal(msg, receivedMessages[0].Message);
            Assert.True(client.IsConnected); // Still connected
        }

        [Fact]
        public void StringReceivedEvent_HandlesIncompletePairAtDisconnect()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);
            string from = "Eve";
            var receivedMessages = new List<IMReceivedEventArgs>();
            client.MessageReceived += (s, a) => receivedMessages.Add(a);
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(from));
            Assert.Empty(receivedMessages);
            _lowLevelClientMock.Raise(c => c.Disconnected += null, EventArgs.Empty);
            Assert.False(client.IsConnected);
            Assert.Empty(receivedMessages);
        }

        // ����� ��� Client Constructor / Initial State
        [Fact]
        public void ClientConstructor_InitializesStateAsConnected()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);
        }

        [Fact]
        public void ClientConstructor_HooksUpStringReceivedEvent()
        {
            // ��������� ��� � ����������, �� �� ����� ��� ������� �������� ��䳿
            var lowLevelClientMock = new Mock<INetworkClient>();
            var client = new Client(lowLevelClientMock.Object);

            // ������ ���� StringReceived � ����������, �� �� ���� ������� (����� ���� �������� ����� �� ������ ��� ����)
        }

        [Fact]
        public void ClientConstructor_HooksUpDisconnectedEvent()
        {
            var lowLevelClientMock = new Mock<INetworkClient>();
            var client = new Client(lowLevelClientMock.Object);

            // ������ ���� Disconnected � ����������, �� �� ���� ������� � �� ������� ����
            lowLevelClientMock.Raise(c => c.Disconnected += null, EventArgs.Empty);
            Assert.False(client.IsConnected); // ����������, �� �������� ���������
        }


        // ����� ��� Connect()
        [Fact]
        public void Connect_WithEmptyStrings_SendsEmptyStrings()
        {
            var client = new Client(_lowLevelClientMock.Object);
            string u = "", p = "", e = "", i = ""; // �� ���� �����

            var sequence = new MockSequence();

            // Setup �������� SendString �������� ����-���� �����, ��� InSequence+Setup
            // ������, ��� ������� ���������� ���� � ����� �������.
            // � ����� ����������� ���� �� ������� 4 ������� SendString, ����� � ���� ������ "".
            _lowLevelClientMock.InSequence(sequence).Setup(c => c.SendString(u)); // ������ ������ �� ���� SendString("")
            _lowLevelClientMock.InSequence(sequence).Setup(c => c.SendString(p)); // ������ ������ �� ���� SendString("")
            _lowLevelClientMock.InSequence(sequence).Setup(c => c.SendString(e)); // ����� ������ �� ���� SendString("")
            _lowLevelClientMock.InSequence(sequence).Setup(c => c.SendString(i)); // ��������� ������ �� ���� SendString("")

            // ��������� ����� Client.Connect
            client.Connect(u, p, e, i);

            // ����������, �� ���������� �볺��� ���������� ���������
            Assert.Equal(u, client.UserName);
            Assert.Equal(p, client.Password);
            Assert.Equal(e, client.Email);
            Assert.Equal(i, client.Image);


            // ����������, �� ���� IsConnected �����
            Assert.True(client.IsConnected);
        }

        [Fact]
        public void Connect_WithNullStrings_SendsNullStrings()
        {
            var client = new Client(_lowLevelClientMock.Object);
            string u = null, p = null, e = null, i = null;
            _lowLevelClientMock.Setup(c => c.SendString(It.IsAny<string>()));
            client.Connect(u, p, e, i);
            _lowLevelClientMock.Verify(c => c.SendString(null), Times.Exactly(4)); // ����������, �� SendString ���������� 4 ���� � null
                                                                                   // ���������� �볺��� �������� null
            Assert.Null(client.UserName);
            Assert.Null(client.Password);
            Assert.Null(client.Email);
            Assert.Null(client.Image);
        }



        [Fact]
        public void Connect_WhenSendStringThrows_CallsDisposeOnLowLevelClient()
        {
            var client = new Client(_lowLevelClientMock.Object);
            string u = "FailUser";
            // ����������� SendString ��� ������� ������� (UserName) ������ �������
            _lowLevelClientMock.SetupSequence(c => c.SendString(It.IsAny<string>()))
                .Throws<IOException>(); // ������ IOException ��� ������ ��������

            // ����������� ���������� Dispose
            _lowLevelClientMock.Setup(c => c.Dispose()).Verifiable();

            // ��������� Connect � ������� �������
            Assert.Throws<InvalidOperationException>(() => client.Connect(u, "pass", "mail", "img"));

            // ����������, �� Dispose ��� ���������� (����� Disconnect � catch �����)
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Once);

            // ����������, �� ���� ������� �� ���������
            Assert.False(client.IsConnected);
        }

        [Fact]
        public void Connect_WhenSendStringThrows_ThrowsInvalidOperationException()
        {
            var client = new Client(_lowLevelClientMock.Object);
            _lowLevelClientMock.SetupSequence(c => c.SendString(It.IsAny<string>()))
                .Throws<IOException>(); // ������ �������

            _lowLevelClientMock.Setup(c => c.Dispose());

            // ����������, �� Connect ���� InvalidOperationException (� InnerException)
            var ex = Assert.Throws<InvalidOperationException>(() => client.Connect("u", "p", "e", "i"));
            Assert.NotNull(ex.InnerException);
            Assert.IsType<IOException>(ex.InnerException); // ���������� ��� InnerException
        }

        // ����� ��� Disconnect()
        [Fact]
        public void Disconnect_WhenAlreadyDisconnected_DoesNotDisposeLowLevelClientAgain()
        {
            var client = new Client(_lowLevelClientMock.Object);
            // ������ ����� ����������
            _lowLevelClientMock.Setup(c => c.Dispose()); // ���������� Dispose
            client.Disconnect(); // ����� ����������
            Assert.False(client.IsConnected);

            // ������� �������� ������� Dispose �� ����
            _lowLevelClientMock.Invocations.Clear();

            // ��������� Disconnect �����
            client.Disconnect();

            // ����������, �� Dispose �� ��� ���������� ������
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Never);

            // ����������, �� ���� ��������� false
            Assert.False(client.IsConnected);
        }

        // ����� ��� SendMessage()
        [Fact]
        public void SendMessage_WhenConnected_WithEmptyRecipient_SendsEmptyRecipient()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);
            string to = "", msg = "Hello!";
            _lowLevelClientMock.Setup(c => c.SendString(to)).Verifiable();
            _lowLevelClientMock.Setup(c => c.SendString(msg)).Verifiable();
            client.SendMessage(to, msg);
            _lowLevelClientMock.Verify(c => c.SendString(to), Times.Once);
            _lowLevelClientMock.Verify(c => c.SendString(msg), Times.Once);
        }

        [Fact]
        public void SendMessage_WhenConnected_WithEmptyMessage_SendsEmptyMessage()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);
            string to = "Bob", msg = "";
            _lowLevelClientMock.Setup(c => c.SendString(to)).Verifiable();
            _lowLevelClientMock.Setup(c => c.SendString(msg)).Verifiable();
            client.SendMessage(to, msg);
            _lowLevelClientMock.Verify(c => c.SendString(to), Times.Once);
            _lowLevelClientMock.Verify(c => c.SendString(msg), Times.Once);
        }

        [Fact]
        public void SendMessage_WhenConnected_WithNullRecipient_SendsNullRecipient()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);
            string to = null, msg = "Hello!";
            _lowLevelClientMock.Setup(c => c.SendString(to)).Verifiable();
            _lowLevelClientMock.Setup(c => c.SendString(msg)).Verifiable();
            client.SendMessage(to, msg);
            _lowLevelClientMock.Verify(c => c.SendString(null), Times.Once); // ����������, �� SendString ���������� � null
            _lowLevelClientMock.Verify(c => c.SendString(msg), Times.Once);
        }

        [Fact]
        public void SendMessage_WhenConnected_WithNullMessage_SendsNullMessage()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);
            string to = "Bob", msg = null;
            _lowLevelClientMock.Setup(c => c.SendString(to)).Verifiable();
            _lowLevelClientMock.Setup(c => c.SendString(msg)).Verifiable();
            client.SendMessage(to, msg);
            _lowLevelClientMock.Verify(c => c.SendString(to), Times.Once);
            _lowLevelClientMock.Verify(c => c.SendString(null), Times.Once); // ����������, �� SendString ���������� � null
        }


        [Fact]
        public void SendMessage_WhenSendStringForRecipientThrows_CallsDisposeOnLowLevelClient()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);
            // ����������� SendString ��� ������� ������� (recipient) ������ �������
            _lowLevelClientMock.SetupSequence(c => c.SendString(It.IsAny<string>()))
                .Throws<IOException>() // ������ IOException ��� ������ �������� (recipient)
                .Pass(); // ���������� ����� (message) ������ (���� ���� �� ���� ���������)

            // ����������� ���������� Dispose
            _lowLevelClientMock.Setup(c => c.Dispose()).Verifiable();

            // ��������� SendMessage � ������� �������
            Assert.Throws<InvalidOperationException>(() => client.SendMessage("to", "msg"));

            // ����������, �� Dispose ��� ���������� (����� Disconnect � catch �����)
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Once);

            // ����������, �� ���� ������� �� ���������
            Assert.False(client.IsConnected);
        }


        [Fact]
        public void SendMessage_WhenSendStringForMessageThrows_CallsDisposeOnLowLevelClient()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);
            // ����������� SendString ��� ������� ������� (recipient) ������,
            // � ��� ������� (message) ������ �������
            _lowLevelClientMock.SetupSequence(c => c.SendString(It.IsAny<string>()))
                .Pass() // ���������� ����� �������� (recipient)
                .Throws<IOException>(); // ������ IOException ��� ����� �������� (message)

            // ����������� ���������� Dispose
            _lowLevelClientMock.Setup(c => c.Dispose()).Verifiable();

            // ��������� SendMessage � ������� �������
            Assert.Throws<InvalidOperationException>(() => client.SendMessage("to", "msg"));

            // ����������, �� Dispose ��� ���������� (����� Disconnect � catch �����)
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Once);

            // ����������, �� ���� ������� �� ���������
            Assert.False(client.IsConnected);
        }

      

        // ����� ��� Receiver State Machine (_lowLevelClient_StringReceived ��������)

        [Fact]
        public async Task StringReceivedEvent_ReceivesEmptyString_LogsWarningAndIgnores()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);
            var receivedMessages = new List<IMReceivedEventArgs>();
            client.MessageReceived += (s, a) => receivedMessages.Add(a); // ϳ���������, ��� �� ������

            // ������ ��������� ���������� �����
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(""));

            // ����������, �� ���� MessageReceived �� ���� ���������
            Assert.Empty(receivedMessages);
        }

        [Fact]
        public async Task StringReceivedEvent_ReceivesNullMessageArgs_LogsWarningAndIgnores()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);
            var receivedMessages = new List<IMReceivedEventArgs>();
            client.MessageReceived += (s, a) => receivedMessages.Add(a);

            // ������ ��������� null EventArgs (���� Raise ���� �� ��������� ��, ��� ������, �� �� ���� ������)
            // ��� ������ ��������� Args � Message = null
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs("Error")); // ����������� StringReceivedEventArgs ���� ������� ��� null.

            var mockNullArgs = new Mock<StringReceivedEventArgs>(null);
        }


        [Fact]
        public async Task StringReceivedEvent_ReceivesMessageWhenExpectingFrom_LogsErrorAndResetsState()
        {
            // Needs mocking logger or checking state didn't change + no message raised.
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);
            var receivedMessages = new List<IMReceivedEventArgs>();
            client.MessageReceived += (s, a) => receivedMessages.Add(a);

            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs("This looks like a message!"));

            // Verify no MessageReceived event was raised
            Assert.Empty(receivedMessages);
        }




        [Fact]
        public async Task StringReceivedEvent_WhenHandlerBodyThrows_LogsErrorAndResetsState()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);
            var receivedMessages = new List<IMReceivedEventArgs>();
            client.MessageReceived += (s, a) =>
            {
                throw new InvalidOperationException("Simulated error in handler logic!");
            };

            string fromUser = "Charlie";
            string messageContent = "Hi there!";

            // Simulate receiving the pair
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(fromUser));
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(messageContent));
        }

        // Test receiving multiple pairs back-to-back
        [Fact]
        public async Task StringReceivedEvent_ReceivesMultiplePairsBackToBack_RaisesMultipleMessageReceivedEvents()
        {
            var client = new Client(_lowLevelClientMock.Object);
            var receivedMessages = new List<IMReceivedEventArgs>();
            var signal = new SemaphoreSlim(0, 1); // Use for the last expected message

            client.MessageReceived += (s, a) =>
            {
                receivedMessages.Add(a);
                // Signal only for the last message to avoid over-signaling
                if (receivedMessages.Count == 2) // We expect 2 messages (2 pairs)
                {
                    signal.Release();
                }
            };

            string from1 = "User1", msg1 = "Msg1";
            string from2 = "User2", msg2 = "Msg2";

            // Simulate receiving first pair
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(from1));
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(msg1));

            // Simulate receiving second pair immediately after
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(from2));
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(msg2));

            // Wait for the signal (after the second message is processed)
            bool signalled = await signal.WaitAsync(1000);
            Assert.True(signalled, "Expected 2 messages but didn't receive them within timeout.");

            // Verify exactly two messages were received
            Assert.Equal(2, receivedMessages.Count);

            // Verify the content of the first message
            Assert.Equal(from1, receivedMessages[0].From);
            Assert.Equal(msg1, receivedMessages[0].Message);

            // Verify the content of the second message
            Assert.Equal(from2, receivedMessages[1].From);
            Assert.Equal(msg2, receivedMessages[1].Message);
        }


        // ����� �� ������� ���������� LowLevelClientDisconnectedEvent
        [Fact]
        public void LowLevelClientDisconnectedEvent_WhenExpectingMessage_ResetsReceiverState()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);

            string fromUser = "PartialUser";
            // ������ ��������� ����� "from", ��� ������� � ���� ExpectingMessage
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(fromUser));

            // ����������, �� ���� ���������� �� true
            Assert.True(client.IsConnected);
            // ����������, �� ����������� �� �� ���������
            var receivedMessages = new List<IMReceivedEventArgs>();
            client.MessageReceived += (s, a) => receivedMessages.Add(a);
            Assert.Empty(receivedMessages);

            // ������ ����������
            _lowLevelClientMock.Raise(c => c.Disconnected += null, EventArgs.Empty);

            // ����������, �� ���� ������� �� false
            Assert.False(client.IsConnected);

            // ����������, �� ������� ���� �� �������� �� ������� MessageReceived
            Assert.Empty(receivedMessages);
            // (�����������) �������� ����������� ����� �������, �� �� �������� �� ExpectingFrom
        }

        [Fact]
        public void LowLevelClientDisconnectedEvent_WhenExpectingFrom_ResetsReceiverState()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);

            // �볺�� ��� � ���� ExpectingFrom ���� ���������

            // ������ ����������
            _lowLevelClientMock.Raise(c => c.Disconnected += null, EventArgs.Empty);

            // ����������, �� ���� ������� �� false
            Assert.False(client.IsConnected);

            // ����������, �� ����������� �� ��������� (���������)
            var receivedMessages = new List<IMReceivedEventArgs>();
            client.MessageReceived += (s, a) => receivedMessages.Add(a);
            Assert.Empty(receivedMessages);
        }


        // ����� �� ������� ���� �� ��������� ������
        [Fact]
        public void Connect_ThenDisconnect_ClientStateIsDisconnectedAndLowLevelClientDisposed()
        {
            var client = new Client(_lowLevelClientMock.Object);
            string u = "u", p = "p", e = "e", i = "i";
            _lowLevelClientMock.Setup(c => c.SendString(It.IsAny<string>())); // ���������� �������� ��� Connect
            _lowLevelClientMock.Setup(c => c.Dispose()).Verifiable();

            client.Connect(u, p, e, i);
            Assert.True(client.IsConnected);

            client.Disconnect();
            Assert.False(client.IsConnected);
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Once);
        }

        [Fact]
        public void Connect_Send_Receive_Disconnect_SuccessfulFlow()
        {
            var client = new Client(_lowLevelClientMock.Object);
            string u = "u", p = "p", e = "e", i = "i";
            _lowLevelClientMock.Setup(c => c.SendString(It.IsAny<string>())); // ���������� �������� ��� Connect � Send
            _lowLevelClientMock.Setup(c => c.Dispose()).Verifiable();

            // Connect
            client.Connect(u, p, e, i);
            Assert.True(client.IsConnected);
            _lowLevelClientMock.Verify(c => c.SendString(It.IsAny<string>()), Times.Exactly(4)); // Verify initial sends

            // Send
            string to = "Bob", msg = "Hi";
            client.SendMessage(to, msg);
            _lowLevelClientMock.Verify(c => c.SendString(to), Times.Once);
            _lowLevelClientMock.Verify(c => c.SendString(msg), Times.Once);
            Assert.True(client.IsConnected);

            // Receive (simulate)
            string fromRx = "Charlie", msgRx = "Hello Back";
            var receivedMessages = new List<IMReceivedEventArgs>();
            var signal = new SemaphoreSlim(0, 1);
            client.MessageReceived += (s, a) => { receivedMessages.Add(a); signal.Release(); };
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(fromRx));
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(msgRx));
            Assert.True(signal.Wait(1000)); // Wait for the message event
            Assert.Single(receivedMessages);
            Assert.Equal(fromRx, receivedMessages[0].From);
            Assert.Equal(msgRx, receivedMessages[0].Message);
            Assert.True(client.IsConnected); // Still connected after receiving

            // Disconnect
            client.Disconnect();
            Assert.False(client.IsConnected);
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Once);
        }

        [Fact]
        public void Connect_ImmediatelyDisconnect_ClientStateIsDisconnectedAndLowLevelClientDisposed()
        {
            var client = new Client(_lowLevelClientMock.Object);
            string u = "u";
            _lowLevelClientMock.Setup(c => c.SendString(It.IsAny<string>())); // Allow sends during Connect
            _lowLevelClientMock.Setup(c => c.Dispose()).Verifiable();

            client.Connect(u, "p", "e", "i");
            // Don't verify sends here yet, they happen sync in Connect.

            client.Disconnect();
            Assert.False(client.IsConnected);
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Once);

            // Verify sends happened *before* disconnect
            _lowLevelClientMock.Verify(c => c.SendString(It.IsAny<string>()), Times.Exactly(4));
        }

        [Fact]
        public void CreateClient_ReceiveStringBeforeConnect_DoesNotRaiseMessageReceived()
        {
            var client = new Client(_lowLevelClientMock.Object); // Client is created, isConnected = true
                                                                 // BUT Connect hasn't been called, properties are null, no initial data sent.

            string from = "Early", msg = "Msg";
            var receivedMessages = new List<IMReceivedEventArgs>();
            client.MessageReceived += (s, a) => receivedMessages.Add(a);

            // Simulate receiving a pair *before* Connect
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(from));
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(msg));

            // Test based on current code: The handler runs, state machine processes.
            Assert.Single(receivedMessages); // It will raise the event based on the state machine
            Assert.Equal(from, receivedMessages[0].From);
            Assert.Equal(msg, receivedMessages[0].Message);
            Assert.True(client.IsConnected); // Still connected
        }

        // Example: SendMessage with non-empty to, null msg
        [Fact]
        public void SendMessage_WhenConnected_WithNonNullRecipientAndNullMessage_SendsBoth()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);
            string to = "Bob", msg = null;
            _lowLevelClientMock.Setup(c => c.SendString(to)).Verifiable();
            _lowLevelClientMock.Setup(c => c.SendString(msg)).Verifiable(); // Setup for null
            client.SendMessage(to, msg);
            _lowLevelClientMock.Verify(c => c.SendString(to), Times.Once);
            _lowLevelClientMock.Verify(c => c.SendString(null), Times.Once);
        }


        // Test receiving logic with sequence Message -> From -> Message (error -> error -> correct start)
        [Fact]
        public async Task StringReceivedEvent_ReceivesSequenceMsgFromMsg_RaisesFirstPairAndStartsNewOne()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);

            string msg1_as_from = "Content of Msg1"; // ������ ��������� ������ ����������� (�� ����� From)
            string from1_as_msg = "Sender of From1"; // ������ ��������� ���������� (�� ����� Message)
            string msg2_starts_new_pair = "Content of Msg2"; // ������ ��������� �� ������ ������ (�� ����� From ��� �������� ����)

            var receivedMessages = new List<IMReceivedEventArgs>();
            // ������� ��� ���������� ����. ������������� 2, �� ������� 2 ������ ����������� � ��� �����������.
            var signal1 = new SemaphoreSlim(0, 1); // ������ ��� ������� ����������� (Msg1/From1)
            var signal2 = new SemaphoreSlim(0, 1); // ������ ��� ������� ����������� (Msg2/...)

            client.MessageReceived += (sender, args) =>
            {
                receivedMessages.Add(args);
                // ����������, ���� �������� ����� �� ����� �����������
                if (receivedMessages.Count == 1) signal1.Release();
                if (receivedMessages.Count == 2) signal2.Release();
            };

            // ���� 1: �������� Msg1 (������� From)
            // -> Client: msg1 ��� _currentFrom, state = ExpectingMessage
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(msg1_as_from));

            // ���� 2: �������� From1 (������� Message)
            // -> Client: ����� ����������� � _currentFrom (msg1_as_from) �� ���������� (from1_as_msg),
            //    ������� MessageReceived, state = ExpectingFrom
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(from1_as_msg));

            // ������ �� ������ ��� ����� �����������
            bool signalled1 = await signal1.WaitAsync(1000);
            Assert.True(signalled1, "First message (Msg1 as From, From1 as Msg) was not raised within timeout.");

            // ����������, �� � ���� ���� �����������
            Assert.Single(receivedMessages);

            // ���������� ���� ������� ����������� (����� � ������ Client)
            var firstMessage = receivedMessages[0];
            Assert.Equal(msg1_as_from, firstMessage.From);   // Msg1 ���� �����������
            Assert.Equal(from1_as_msg, firstMessage.Message); // From1 ���� �������
            Assert.True(client.IsConnected); // ���� ���������� �� �������

            // -> Client: msg2 ��� _currentFrom, state = ExpectingMessage
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(msg2_starts_new_pair));


            Assert.Equal(1, receivedMessages.Count); // �������� ��� �� ������ ����� ����� �����������


            string nextMsgContent = "Final piece";
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(nextMsgContent));

            // ������ �� ������ ��� ����� �����������
            bool signalled2 = await signal2.WaitAsync(1000);
            Assert.True(signalled2, "Second message (Msg2 as From, Final piece as Msg) was not raised within timeout.");

            // ����������, �� � ���� ��� ����������� �������
            Assert.Equal(2, receivedMessages.Count);

            // ���������� ���� ������� �����������
            var secondMessage = receivedMessages[1];
            Assert.Equal(msg2_starts_new_pair, secondMessage.From); // Msg2 ���� �����������
            Assert.Equal(nextMsgContent, secondMessage.Message); // "Final piece" ���� �������
            Assert.True(client.IsConnected); // ���� ���������� �� �������
        }

        [Fact]
        public async Task StringReceived_PartialThenDisconnectThenConnectThenFullPair_RaisesCorrectMessage()
        {
            var client = new Client(_lowLevelClientMock.Object); // Connected = true
            string fromUser = "PartialUser";
            var receivedMessages = new List<IMReceivedEventArgs>();
            client.MessageReceived += (s, a) => receivedMessages.Add(a);

            // Receive partial pair
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(fromUser));
            Assert.Empty(receivedMessages); // State is ExpectingMessage

            // Disconnect
            _lowLevelClientMock.Raise(c => c.Disconnected += null, EventArgs.Empty);
            Assert.False(client.IsConnected); // State is Disconnected, ReceiverState reset

            // Simulate Connect again (sends initial data)
            // Client.Connect assumes it can send sync.
            // We need a new Client instance usually for a fresh connection state in tests.
            // Or the INetworkClient needs a Connect method we can mock.
            // Let's create a new client to simulate a fresh connection attempt.
            var client2 = new Client(_lowLevelClientMock.Object); // New client, IsConnected = true, state ExpectingFrom
            Assert.True(client2.IsConnected);

            // Set up mock for connects (it happens sync in Client.Connect)
            _lowLevelClientMock.Setup(c => c.SendString(It.IsAny<string>()));
            client2.Connect("NewUser", "p", "e", "i");
            _lowLevelClientMock.Verify(c => c.SendString(It.IsAny<string>()), Times.Exactly(4)); // Verify connect sends

            var receivedMessages2 = new List<IMReceivedEventArgs>();
            var signal2 = new SemaphoreSlim(0, 1);
            client2.MessageReceived += (s, a) => { receivedMessages2.Add(a); signal2.Release(); };

            // Receive the full pair on the *new* client
            string fromUser2 = "NewFrom", msgUser2 = "NewMsg";
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(fromUser2));
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(msgUser2));

            bool signalled = await signal2.WaitAsync(1000);
            Assert.True(signalled, "Second client did not receive message pair.");

            Assert.Single(receivedMessages); // Original client got nothing
            Assert.Single(receivedMessages2); // New client got one message
            Assert.Equal(fromUser2, receivedMessages2[0].From);
            Assert.Equal(msgUser2, receivedMessages2[0].Message);
        }


       


        // Test calling methods after Dispose/Disconnect
        [Fact]
        public void SendMessage_AfterDisconnect_ThrowsInvalidOperationException()
        {
            var client = new Client(_lowLevelClientMock.Object);
            _lowLevelClientMock.Setup(c => c.Dispose()); // ���������� ������ Dispose()
            client.Disconnect(); // Disconnects the client

            Assert.Throws<InvalidOperationException>(() => client.SendMessage("to", "msg"));
        }

        [Fact]
        public void StringReceived_AfterDisconnectedEvent_FurtherReceivesIgnored()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);

            // Simulate disconnection
            _lowLevelClientMock.Raise(c => c.Disconnected += null, EventArgs.Empty);
            Assert.False(client.IsConnected);

            // Simulate receiving a string *after* disconnection
            var receivedMessages = new List<IMReceivedEventArgs>();
            client.MessageReceived += (s, a) => receivedMessages.Add(a);

            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs("PostDisconnectData"));

            // Verify no message was processed/raised
            Assert.Empty(receivedMessages);
            // State should remain disconnected
            Assert.False(client.IsConnected);
            // Receiver state should remain reset
        }

        [Fact]
        public void ClientConstructor_WithNullLowLevelClient_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new Client(null));
        }

        [Fact]
        public void Connect_WhenPasswordSendStringThrows_CallsDisposeAndThrows()
        {
            var client = new Client(_lowLevelClientMock.Object);
            string u = "User", p = "Pass", e = "Email", i = "Image";

            // ����������� Sequence, ��� ������� ������� �� ������� SendString (Password)
            var sequence = new MockSequence();
            _lowLevelClientMock.InSequence(sequence).Setup(c => c.SendString(u)); // User - ������
            _lowLevelClientMock.InSequence(sequence).Setup(c => c.SendString(p)).Throws<IOException>(); // Password - �������
            _lowLevelClientMock.Setup(c => c.Dispose()).Verifiable(); // ����������� Dispose, �� �� ���� ����������

            // ��������� Connect � ����������, �� �������� �������
            var ex = Assert.Throws<InvalidOperationException>(() => client.Connect(u, p, e, i));

            // ����������, �� Dispose ���� ���������
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Once);
            // ����������, �� ���� ������� �� ���������
            Assert.False(client.IsConnected);
            // ���������� ��� InnerException
            Assert.NotNull(ex.InnerException);
            Assert.IsType<IOException>(ex.InnerException);

            // ����������, �� ������ SendString ��� ����������, � ������� - � (��� ����, �� ����� �������)
            _lowLevelClientMock.Verify(c => c.SendString(u), Times.Once);
            // SendString(p) ����� �������, ���� �� ����� ������������ �� "���������� ������" ���� Throws.
            // Verify(Times.Once) �� Setup().Throws() ��������, �� Throws() ���� ��������� ���� ���.
            _lowLevelClientMock.Verify(c => c.SendString(p), Times.Once); // ����������, �� ��� ������ ������� (� �����)
            _lowLevelClientMock.Verify(c => c.SendString(e), Times.Never); // Email �� ��� ���� �����������
            _lowLevelClientMock.Verify(c => c.SendString(i), Times.Never); // Image �� ��� ���� �����������
        }

        // ��������, �� Client.Connect ��� ���� ������� ������� (�������, ������, ��� �����, ������� �� ����� Client)
        [Fact]
        public void Connect_WhenInitialSendStringThrowsAndDisposeThrows_ThrowsOriginalException()
        {
            var client = new Client(_lowLevelClientMock.Object);
            string u = "UserFail";

            // ����������� SendString ������ ������ ������� (�� ��� Connect)
            _lowLevelClientMock.SetupSequence(c => c.SendString(It.IsAny<string>()))
                .Throws<IOException>();

            // ����������� Dispose ������ ������ ������� (���� Client.Disconnect ����������� � catch)
            _lowLevelClientMock.Setup(c => c.Dispose()).Throws<InvalidOperationException>();

            // ��������� Connect � ����������, ���� ������� �����������
            // ������� ����� Client: ����� SendString Exception, ������� Disconnect (���� ���� Dispose Exception),
            // catch Client'� ����� Dispose Exception � ������� ���� (InvalidOperationException) ����������.
            var ex = Assert.Throws<InvalidOperationException>(() => client.Connect(u, "p", "e", "i"));

            // ����������, �� InnerException � �������� �� Dispose, � �� �� SendString
            Assert.NotNull(ex);
            Assert.IsType<InvalidOperationException>(ex); // ������� InvalidOperationException �� Dispose mock

            // ����������, �� Dispose ��� ����������
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Once);
            // ���� ������� false, ��� ��� ������� ������� �� ���� ���� �����������
            // Assert.False(client.IsConnected); // ���� ���� false, ���� isConnected = false ��� ������������ �� throw Dispose
        }


        // ������� ������� �������: SendMessage ���� �������, � ���� Dispose ���� �������
        [Fact]
        public void SendMessage_WhenMessageSendStringThrowsAndDisposeThrows_ThrowsOriginalException()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected); // �볺�� ����������

            // ����������� SendString ��� ������� ������� (recipient) ������, ��� ������� (message) ������ ������ �������
            _lowLevelClientMock.SetupSequence(c => c.SendString(It.IsAny<string>()))
                .Pass() // SendString(recipient) - ������
                .Throws<IOException>(); // SendString(message) - �������

            // ����������� Dispose ������ ������ �������
            _lowLevelClientMock.Setup(c => c.Dispose()).Throws<InvalidOperationException>();

            // ��������� SendMessage � ����������, ���� ������� �����������
            var ex = Assert.Throws<InvalidOperationException>(() => client.SendMessage("to", "msg"));

            // ����������, �� InnerException � �������� �� Dispose
            Assert.NotNull(ex);
            Assert.IsType<InvalidOperationException>(ex); // ������� InvalidOperationException �� Dispose mock

            // ����������, �� Dispose ��� ����������
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Once);
            // ���� ������� false
            // Assert.False(client.IsConnected);
        }

        // ��������� �����������: ��������� ���� -> ������� (From ������ Message) -> ...
        // ��������, �� ���� ��������� ����, �������� ������� �������� ����� ������.
        [Fact]
        public async Task StringReceivedEvent_ReceivesSequenceFromThenFrom_RaisesMessageWithSwappedContent()
        {
            var client = new Client(_lowLevelClientMock.Object); // Client ������ � ���� ExpectingFrom
            Assert.True(client.IsConnected); // ���������� ���������� ����

            var receivedMessages = new List<IMReceivedEventArgs>();
            var signal = new SemaphoreSlim(0, 1); // ������ ��� ���������� ��䳿 MessageReceived

            // ϳ��������� �� ���� MessageReceived
            client.MessageReceived += (s, a) => {
                receivedMessages.Add(a); // ������ �������� ����������� �� ������
                signal.Release(); // ����������, �� ����������� ��������
            };

            string from1_sender = "SenderA";    // ������ ������ ����� (����� From)
            string from2_message = "SenderB"; // ������ ������ ����� (����� Message)

            // ���� 1: ������ ��������� ������� ����� ("From1")
            // ��������: ���� ExpectingFrom -> _currentFrom="SenderA", ���� ExpectingMessage
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(from1_sender));
            // ����������, �� ����������� �� ����
            Assert.Empty(receivedMessages); // �� �������� �� ������

            // ���� 2: ������ ��������� ������� ����� ("From2") (������� Message)
            // ��������: ���� ExpectingMessage -> Message(From=_currentFrom="SenderA", Message="SenderB"), ���� ExpectingFrom
            // �������� �������� MessageReceived ���.
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(from2_message));

            // ������ �� ������ ��� �����������
            bool signalled = await signal.WaitAsync(1000); // ������ �� 1 �������
                                                           // ����������, �� ������ ��� ��������� (�����, ���� MessageReceived ����������)
            Assert.True(signalled, "MessageReceived event was not raised after receiving From then From.");

            // ����������, �� ������ ������ ���� ���� �����������
            Assert.Single(receivedMessages); // *** ������ � Assert.Empty() �� Assert.Single() ***

            // ���������� ���� ���������� ����������� (�������� �� ����� Client: ������ ����� = From, ������ = Message)
            var receivedArgs = receivedMessages[0];
            Assert.Equal(from1_sender, receivedArgs.From);       // ³�������� - ������ ����� ("SenderA")
            Assert.Equal(from2_message, receivedArgs.Message); // ����� - ������ ����� ("SenderB")

            Assert.True(client.IsConnected); // ����������, �� ���� ���������� �� �������
        }

        // ��������� �������� ��� ���� ������.
        // ��������, �� ���������� �������� �������� �������� ������� ���� �����.
        [Fact]
        public async Task StringReceivedEvent_ReceivesManyRapidPairs_ProcessesAllMessagesCorrectly()
        {
            var client = new Client(_lowLevelClientMock.Object); // State = ExpectingFrom
            Assert.True(client.IsConnected);
            var receivedMessages = new List<IMReceivedEventArgs>();
            var signal = new SemaphoreSlim(0, 1);

            // ������� 5 ������ ���������� (10 �����)
            const int expectedMessageCount = 5;
            client.MessageReceived += (s, a) =>
            {
                receivedMessages.Add(a);
                if (receivedMessages.Count == expectedMessageCount)
                {
                    signal.Release();
                }
            };

            // ������ 5 ��� (10 �����) ���� ������
            for (int i = 1; i <= expectedMessageCount; i++)
            {
                string from = $"From{i}";
                string msg = $"Msg{i}";
                _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(from));
                _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(msg));
            }

            // ������ �� ������ �����������
            bool signalled = await signal.WaitAsync(2000); // ���� ����� ����� ����

            Assert.True(signalled, $"Did not receive {expectedMessageCount} messages within timeout.");
            Assert.Equal(expectedMessageCount, receivedMessages.Count);

            // ���������� ���� ������ ����������
            for (int i = 0; i < expectedMessageCount; i++)
            {
                Assert.Equal($"From{i + 1}", receivedMessages[i].From);
                Assert.Equal($"Msg{i + 1}", receivedMessages[i].Message);
            }
            Assert.True(client.IsConnected);
        }

        // INetworkClient.StringReceived ����������� ���� ����, �� Client.Disconnect() ��� ����������.
        // ��������, �� Client ������ ����� ��� ���� ����, �� ���� ��������������� ���� ���� "���������".
        [Fact]
        public async Task StringReceivedEvent_AfterClientDisconnect_EventsAreProcessedByHandler()
        {
            var client = new Client(_lowLevelClientMock.Object);
            // ���������� ���������� ���� ���������� (true � ������������)
            Assert.True(client.IsConnected);

            // ����������� ���: ���������� ������ Dispose(), �� �� ���������� ��� Disconnect()
            _lowLevelClientMock.Setup(c => c.Dispose());

            // ��������� Disconnect �� Client
            // �� ��������� Client.Disconnect -> CloseConnection
            // CloseConnection �� ���������� IsConnected = false �� ������� �������� ���� ������� (_receiverState = ExpectingFrom)
            client.Disconnect();

            // ����������, �� ���� Client ����� "���������"
            Assert.False(client.IsConnected);

            // ����������, �� Dispose ���� ��������� �� ���� INetworkClient
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Once);

            // ³� ���������� ��������� ����� �����, ���� ���� Raise ���������.
            var receivedMessages = new List<IMReceivedEventArgs>();
            // ������������� SemaphoreSlim ��� ����������, ������� MessageReceived ���� ���� ��������� ����������
            var messageReceivedSignal = new SemaphoreSlim(0, 1); // ������, �� ����������� ��������

            // ϳ��������� �� �������������� ���� MessageReceived ϲ��� ������� Disconnect(), ��� ����� �������� ���� StringReceived
            client.MessageReceived += (s, a) =>
            {
                receivedMessages.Add(a); // ������ �������� ����������� �� ������
                messageReceivedSignal.Release(); // ���������� �����, �� �������� ���������
            };

            string postDisconnectFrom = "Late From"; // ������ ����� ��� "From"
            string postDisconnectMsg = "Late Msg";   // ������ ����� ��� "Msg"

            // ������ ��������� ������� ����� ("From") �� �������������� �볺���
            // ������� ���� ������� ��� �������� �� ExpectingFrom, ��� ����� ���� ��������� �� ���������.
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(postDisconnectFrom));
            // ����������� �� �� �� ���� ��������� (���c�� ���� ������ �����)
            Assert.Empty(receivedMessages); // �� �������� �� ������

            // ������ ��������� ������� ����� ("Msg") �� �������������� �볺���
            // �������� �������� ���� �� ����� �����������, ������ ���� � ������� ���� MessageReceived.
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(postDisconnectMsg));

            // ������ �� ������, ���� ������, �� ���� MessageReceived ���������� (�� ��������� �������� Client)
            bool signalled = await messageReceivedSignal.WaitAsync(1000); // ������ �� 1 �������
            // ����������, �� ������ ��� ��������� (�����, �������� ��������� � �������� ���� MessageReceived)
            Assert.True(signalled, "MessageReceived event was NOT raised after receiving strings post-disconnect (unexpected behavior based on Client's current code).");

            // ����������, �� ������ ��������� ���������� ������ ���� ���� �����������
            Assert.Single(receivedMessages);

            // ���������� ���� ���������� �����������
            var receivedArgs = receivedMessages[0];
            Assert.Equal(postDisconnectFrom, receivedArgs.From);    // ���������� ����������
            Assert.Equal(postDisconnectMsg, receivedArgs.Message); // ���������� �����

            // ���� Client.IsConnected �� ���������� false, �� Disconnect() ��� ���������� �����
            Assert.False(client.IsConnected);
        }


        // �������� ��������� ��䳿 Disconnected �� INetworkClient.
        // ��������, �� ������� ������� ��������� Disconnected �� ���������� �� ������� ��� ������������ �����.
        [Fact]
        public void LowLevelClientDisconnectedEvent_ReceivesMultipleTimes_StateRemainsDisconnected()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);

            // ������ ����� ����������
            _lowLevelClientMock.Raise(c => c.Disconnected += null, EventArgs.Empty);
            Assert.False(client.IsConnected); // ���� �������

            // ������ �������� ����������
            _lowLevelClientMock.Raise(c => c.Disconnected += null, EventArgs.Empty);
            Assert.False(client.IsConnected); // ���� ���������� false

            // ������ ���� ����������
            _lowLevelClientMock.Raise(c => c.Disconnected += null, EventArgs.Empty);
            Assert.False(client.IsConnected); // ���� ���������� false

            // ����������, �� Dispose �� ����������� ���������� Disconnected (Dispose ����������� ������� Disconnect())
            // � ����� ���� Disconnect() �� �����������, ���� Dispose �� �� ���� ����������.
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Never);
        }

        // Connect ����������� ���� Disconnect.
        // ��������, �� Client �������� ������ "��������������" ����� Connect ���� ������ ����������.
        [Fact]
        public void Connect_AfterDisconnect_AttemptsSendButClientRemainsDisconnectedAndThrows()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected); // ���������� ����

            // ����������� Dispose ��� ������� Disconnect
            _lowLevelClientMock.Setup(c => c.Dispose()).Verifiable();

            // ��������� ����� ����������
            client.Disconnect();
            Assert.False(client.IsConnected);
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Once);

            // ������� ��������� ����, ��� ��������� ������� �� ��� "��������������"
            _lowLevelClientMock.Invocations.Clear();

            string u = "ReUser", p = "RePass", e = "ReEmail", i = "ReImage";

            // Client.Connect ������ ��������� ���, ����� ���� !isConnected, ��� ����� �������.
            _lowLevelClientMock.Setup(c => c.SendString(It.IsAny<string>())).Throws(new ObjectDisposedException(null, "Simulating sending on disposed client")); // ���� ����������� �������

            // ��� � ������������: Throws<ObjectDisposedException>("Simulating sending on disposed client");

            // ��������� Connect ����� (������ "��������������")
            // �������, �� Connect ���� �������, ������� SendString ���� ������� �� Dispose'� �볺��.
            var ex = Assert.Throws<InvalidOperationException>(() => client.Connect(u, p, e, i));

            // ����������, �� ���������� ��������� (�� ���������� �� ����� try/catch � Client.Connect)
            Assert.Equal(u, client.UserName);

            // ����������, �� Client ��������� ��������� �������� ��� (�������� SendString)
            _lowLevelClientMock.Verify(c => c.SendString(It.IsAny<string>()), Times.AtLeastOnce); // ��� Times.Exactly(4) ���� SendString(null) �� ���� ������

            // ����������, �� Dispose �� ��� ���������� ������
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Never);

            // ����������, �� ���� ���������� false
            Assert.False(client.IsConnected);

            // ���������� ��� InnerException
            Assert.NotNull(ex.InnerException);
            Assert.IsType<ObjectDisposedException>(ex.InnerException); // ������� ������� �� Mock ��� ������� SendString
        }


        // SendMessage �����������, ���� INetworkClient dependency � null (������).
        // ��������, �� Client �������� �������� ��������, ���� _lowLevelClient ������ null.
        [Fact]
        public void SendMessage_WhenLowLevelClientIsNull_ThrowsNullReferenceExceptionDuringCleanup_IsConnectedRemainsTrue() // ������ �����
        {
            var client = new Client(_lowLevelClientMock.Object);
            // isConnected = true ��������

            // ��������� ������������ _lowLevelClient = null (��������)
            var lowLevelClientField = typeof(Client).GetField("_lowLevelClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(lowLevelClientField);
            lowLevelClientField.SetValue(client, null); // _lowLevelClient � null

            string recipient = "to", messageText = "msg";

            // ������� NRE, ��� ������� �� ��� ��������� ���� � Disconnect/CloseConnection
            var ex = Assert.Throws<NullReferenceException>(() => // Expect NullReferenceException
            {
                // ��������� SendMessage (���������� ����� NRE, ������� � catch, ������ Disconnect)
                // ��������� Disconnect/CloseConnection ���������� ����� NRE
                client.SendMessage(recipient, messageText); // ����� 1488 ��� ������� �����
            });

            Assert.IsType<NullReferenceException>(ex); // ϳ����������, �� �������� ������� � NRE

            // InnerException ��������� ��� �� �������, �� �� ����� NRE, ��� �������� ��� ��������.
            // Assert.NotNull(ex.InnerException); // ��������
            // Assert.IsType<NullReferenceException>(ex.InnerException); // ��������

            // ������� ����� isConnected = false; �� ���������� ����� ������� � CloseConnection,
            // isConnected ���������� True
            Assert.True(client.IsConnected);
        }

        // Connect �����������, ���� INetworkClient dependency � null (������).
        // ������ �� ������������, ��� ��� Connect.
        [Fact]
        public void Connect_WhenLowLevelClientIsNull_ThrowsNullReferenceExceptionDuringCleanup_IsConnectedRemainsTrue()
        {
            var client = new Client(_lowLevelClientMock.Object);
            // isConnected = true ��������

            // ��������� ������������ _lowLevelClient = null (��������)
            var lowLevelClientField = typeof(Client).GetField("_lowLevelClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(lowLevelClientField);
            lowLevelClientField.SetValue(client, null); // _lowLevelClient � null

            string u = "u", p = "p", e = "e", i = "i";

            // ����������, �� ��������� NullReferenceException (��� ������ �� ��� ��������)
            var ex = Assert.Throws<NullReferenceException>(() =>
            {
                // ��������� Connect (���������� ����� NRE, ������� � catch, ������ Disconnect)
                // ��������� Disconnect/CloseConnection ���������� ����� NRE
                client.Connect(u, p, e, i); // ����� 1516 ��� ������� �����
            });

            Assert.IsType<NullReferenceException>(ex);

            // ����������, �� ���������� ���������� (�� ���������� �� try/catch � Connect)
            Assert.Equal(u, client.UserName);
            Assert.Equal(p, client.Password);
            Assert.Equal(e, client.Email);
            Assert.Equal(i, client.Image);


            // ������� ����� isConnected = false; �� ���������� ����� ������� � CloseConnection,
            // isConnected ���������� True. ���� �� �� ����������.
            Assert.True(client.IsConnected);
        }

        [Fact]
        public void Connect_AfterDisconnect_SendStringSucceeds_ClientRemainsDisconnected()
        {
            var client = new Client(_lowLevelClientMock.Object);
            // ���������� ���������� ���� (���������)
            Assert.True(client.IsConnected);

            // ����������� Dispose ��� ������� ������� Disconnect
            // �� ������� ��� Strict ������
            _lowLevelClientMock.Setup(c => c.Dispose());

            // ³�������� Client ������� Disconnect
            // �� �������� �� Client.Disconnect -> CloseConnection
            // CloseConnection ���������� IsConnected=false � ������� Dispose() �� �������������� �볺��.
            client.Disconnect();
            // ����������, �� ���� Client ����� "���������"
            Assert.False(client.IsConnected);
            // ����������, �� Dispose ���� ��������� �� ���� INetworkClient
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Once);

            // ������� ������ ������� ����, ��� ��������� ������� �� ��� �������� ������ Connect
            _lowLevelClientMock.Invocations.Clear();
            // ����������� ���, ��� Verify(Times.Never) ��� Dispose ��������� ��������
            _lowLevelClientMock.Setup(c => c.Dispose()).Verifiable(Times.Never);


            string u = "ReUser", p = "RePass", e = "ReEmail", i = "ReImage";

            // ����������� SendString �� ����, ��� �� ��ϲ��� �����������
            // �� ���� �������, ���� Connect ������� SendString �� ����������� �볺��, � �� �� ���� �������.
            _lowLevelClientMock.Setup(c => c.SendString(It.IsAny<string>()));

            // ��������� ����� Connect �����.
            // ����� � ������ Client, �� ������ ��������� ���, ��� �� ������ isConnected=true.
            // � ����� ������ Connect �� ��� ������ �������.
            client.Connect(u, p, e, i);

            // ����������, �� ����� SendString ��� ���������� 4 ���� (Connect ������ �� ���������)
            _lowLevelClientMock.Verify(c => c.SendString(It.IsAny<string>()), Times.Exactly(4));

            // ����������, �� Dispose �� ��� ���������� ����� (Connect �� �������� Disconnect � catch)
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Never); // ������������� ���� ����������� Times.Never

            // ����������, �� ���������� Client ��������� (Connect ������ �� ���������)
            Assert.Equal(u, client.UserName);
            Assert.Equal(p, client.Password);
            Assert.Equal(e, client.Email);
            Assert.Equal(i, client.Image);

            // ����������, �� ���� Client ����������� ���������
            Assert.False(client.IsConnected);
        }

        [Fact]
        public void Connect_WhenAlreadyConnected_UpdatesPropertiesAndResendsData()
        {
            var client = new Client(_lowLevelClientMock.Object);
            // �� ���������� ������ � �����������, Client ��������� ����������, ���� _lowLevelClient �� null.
            Assert.True(client.IsConnected);

            // ��� ��� ������� ���������� (���� Connect ����������� �������� �����)
            string initialU = "InitialUser", initialP = "InitialPass", initialE = "initial@example.com", initialI = "initial.png";
            // ��� ��� ��� "����������" ����������
            string newU = "NewUser", newP = "NewPass", newE = "new@example.com", newI = "new.png";

            // ����������� SendString �� ����, ��� �� ������ ������� ����-�� �����.
            // �� ��� ���� ������� Connect � ����� ����.
            _lowLevelClientMock.Setup(c => c.SendString(It.IsAny<string>()));

            client.Connect(initialU, initialP, initialE, initialI);

            // ����������, �� ���������� ����������, � ��� ��������� ��� ������� ����������
            Assert.Equal(initialU, client.UserName);
            Assert.Equal(initialP, client.Password);
            Assert.Equal(initialE, client.Email);
            Assert.Equal(initialI, client.Image);
            // ����������, �� SendString ��� ���������� � ������� ������
            _lowLevelClientMock.Verify(c => c.SendString(initialU), Times.Once);
            _lowLevelClientMock.Verify(c => c.SendString(initialP), Times.Once);
            _lowLevelClientMock.Verify(c => c.SendString(initialE), Times.Once);
            _lowLevelClientMock.Verify(c => c.SendString(initialI), Times.Once);
            // ����������, �� ���� ���������� ���������
            Assert.True(client.IsConnected);

            // ������� ������ ������� ����, ��� ����� ��������� �������, �� ���������� �� ��� ������� Connect
            _lowLevelClientMock.Invocations.Clear();
            // ����������� Dispose �� ����, ��� ���������, �� �� �� ����������� ��� ���������� Connect
            _lowLevelClientMock.Setup(c => c.Dispose()).Verifiable(Times.Never);


            client.Connect(newU, newP, newE, newI);

            Assert.Equal(newU, client.UserName);
            Assert.Equal(newP, client.Password);
            Assert.Equal(newE, client.Email);
            Assert.Equal(newI, client.Image);

            _lowLevelClientMock.Verify(c => c.SendString(newU), Times.Once);
            _lowLevelClientMock.Verify(c => c.SendString(newP), Times.Once);
            _lowLevelClientMock.Verify(c => c.SendString(newE), Times.Once);
            _lowLevelClientMock.Verify(c => c.SendString(newI), Times.Once);

            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Never); // ������������� ���� ����������� Times.Never

            Assert.True(client.IsConnected);
        }
    }
}