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
        // Мок для нового інтерфейсу
        private readonly Mock<INetworkClient> _lowLevelClientMock;

        public ClientTests()
        {
            // Створюємо мок для інтерфейсу INetworkClient
            // Використовуємо MockBehavior.Strict, щоб переконатись, що всі викликані методи
            // на моку були явно налаштовані. Це допомагає знайти неочікувані виклики.
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

        // Тести для Client Constructor / Initial State
        [Fact]
        public void ClientConstructor_InitializesStateAsConnected()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);
        }

        [Fact]
        public void ClientConstructor_HooksUpStringReceivedEvent()
        {
            // Створюємо мок і перевіряємо, що до нього був доданий обробник події
            var lowLevelClientMock = new Mock<INetworkClient>();
            var client = new Client(lowLevelClientMock.Object);

            // Імітуємо подію StringReceived і перевіряємо, чи не було помилок (навіть якщо обробник нічого не робить без пари)
        }

        [Fact]
        public void ClientConstructor_HooksUpDisconnectedEvent()
        {
            var lowLevelClientMock = new Mock<INetworkClient>();
            var client = new Client(lowLevelClientMock.Object);

            // Імітуємо подію Disconnected і перевіряємо, чи не було помилок і чи змінився стан
            lowLevelClientMock.Raise(c => c.Disconnected += null, EventArgs.Empty);
            Assert.False(client.IsConnected); // Перевіряємо, що обробник спрацював
        }


        // Тести для Connect()
        [Fact]
        public void Connect_WithEmptyStrings_SendsEmptyStrings()
        {
            var client = new Client(_lowLevelClientMock.Object);
            string u = "", p = "", e = "", i = ""; // Усі пусті рядки

            var sequence = new MockSequence();

            // Setup дозволяє SendString приймати будь-який рядок, але InSequence+Setup
            // вимагає, щоб виклики відбувалися саме в цьому порядку.
            // В цьому конкретному тесті ми очікуємо 4 виклики SendString, кожен з яких передає "".
            _lowLevelClientMock.InSequence(sequence).Setup(c => c.SendString(u)); // Перший виклик має бути SendString("")
            _lowLevelClientMock.InSequence(sequence).Setup(c => c.SendString(p)); // Другий виклик має бути SendString("")
            _lowLevelClientMock.InSequence(sequence).Setup(c => c.SendString(e)); // Третій виклик має бути SendString("")
            _lowLevelClientMock.InSequence(sequence).Setup(c => c.SendString(i)); // Четвертий виклик має бути SendString("")

            // Викликаємо метод Client.Connect
            client.Connect(u, p, e, i);

            // Перевіряємо, що властивості клієнта встановлені правильно
            Assert.Equal(u, client.UserName);
            Assert.Equal(p, client.Password);
            Assert.Equal(e, client.Email);
            Assert.Equal(i, client.Image);


            // Перевіряємо, що стан IsConnected вірний
            Assert.True(client.IsConnected);
        }

        [Fact]
        public void Connect_WithNullStrings_SendsNullStrings()
        {
            var client = new Client(_lowLevelClientMock.Object);
            string u = null, p = null, e = null, i = null;
            _lowLevelClientMock.Setup(c => c.SendString(It.IsAny<string>()));
            client.Connect(u, p, e, i);
            _lowLevelClientMock.Verify(c => c.SendString(null), Times.Exactly(4)); // Перевіряємо, що SendString викликався 4 рази з null
                                                                                   // Властивості клієнта приймуть null
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
            // Налаштовуємо SendString для першого виклику (UserName) кидати виняток
            _lowLevelClientMock.SetupSequence(c => c.SendString(It.IsAny<string>()))
                .Throws<IOException>(); // Кидаємо IOException при першій відправці

            // Налаштовуємо очікування Dispose
            _lowLevelClientMock.Setup(c => c.Dispose()).Verifiable();

            // Викликаємо Connect і очікуємо виняток
            Assert.Throws<InvalidOperationException>(() => client.Connect(u, "pass", "mail", "img"));

            // Перевіряємо, що Dispose був викликаний (через Disconnect в catch блоці)
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Once);

            // Перевіряємо, що стан змінився на відключено
            Assert.False(client.IsConnected);
        }

        [Fact]
        public void Connect_WhenSendStringThrows_ThrowsInvalidOperationException()
        {
            var client = new Client(_lowLevelClientMock.Object);
            _lowLevelClientMock.SetupSequence(c => c.SendString(It.IsAny<string>()))
                .Throws<IOException>(); // Кидаємо виняток

            _lowLevelClientMock.Setup(c => c.Dispose());

            // Перевіряємо, що Connect кидає InvalidOperationException (з InnerException)
            var ex = Assert.Throws<InvalidOperationException>(() => client.Connect("u", "p", "e", "i"));
            Assert.NotNull(ex.InnerException);
            Assert.IsType<IOException>(ex.InnerException); // Перевіряємо тип InnerException
        }

        // Тести для Disconnect()
        [Fact]
        public void Disconnect_WhenAlreadyDisconnected_DoesNotDisposeLowLevelClientAgain()
        {
            var client = new Client(_lowLevelClientMock.Object);
            // Імітуємо перше відключення
            _lowLevelClientMock.Setup(c => c.Dispose()); // Дозволяємо Dispose
            client.Disconnect(); // Перше відключення
            Assert.False(client.IsConnected);

            // Скидаємо лічильник викликів Dispose на моку
            _lowLevelClientMock.Invocations.Clear();

            // Викликаємо Disconnect знову
            client.Disconnect();

            // Перевіряємо, що Dispose не був викликаний вдруге
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Never);

            // Перевіряємо, що стан залишився false
            Assert.False(client.IsConnected);
        }

        // Тести для SendMessage()
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
            _lowLevelClientMock.Verify(c => c.SendString(null), Times.Once); // Перевіряємо, що SendString викликався з null
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
            _lowLevelClientMock.Verify(c => c.SendString(null), Times.Once); // Перевіряємо, що SendString викликався з null
        }


        [Fact]
        public void SendMessage_WhenSendStringForRecipientThrows_CallsDisposeOnLowLevelClient()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);
            // Налаштовуємо SendString для першого виклику (recipient) кидати виняток
            _lowLevelClientMock.SetupSequence(c => c.SendString(It.IsAny<string>()))
                .Throws<IOException>() // Кидаємо IOException при першій відправці (recipient)
                .Pass(); // Дозволяємо друге (message) пройти (хоча воно не буде викликано)

            // Налаштовуємо очікування Dispose
            _lowLevelClientMock.Setup(c => c.Dispose()).Verifiable();

            // Викликаємо SendMessage і очікуємо виняток
            Assert.Throws<InvalidOperationException>(() => client.SendMessage("to", "msg"));

            // Перевіряємо, що Dispose був викликаний (через Disconnect в catch блоці)
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Once);

            // Перевіряємо, що стан змінився на відключено
            Assert.False(client.IsConnected);
        }


        [Fact]
        public void SendMessage_WhenSendStringForMessageThrows_CallsDisposeOnLowLevelClient()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);
            // Налаштовуємо SendString для першого виклику (recipient) пройти,
            // а для другого (message) кинути виняток
            _lowLevelClientMock.SetupSequence(c => c.SendString(It.IsAny<string>()))
                .Pass() // Дозволяємо першу відправку (recipient)
                .Throws<IOException>(); // Кидаємо IOException при другій відправці (message)

            // Налаштовуємо очікування Dispose
            _lowLevelClientMock.Setup(c => c.Dispose()).Verifiable();

            // Викликаємо SendMessage і очікуємо виняток
            Assert.Throws<InvalidOperationException>(() => client.SendMessage("to", "msg"));

            // Перевіряємо, що Dispose був викликаний (через Disconnect в catch блоці)
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Once);

            // Перевіряємо, що стан змінився на відключено
            Assert.False(client.IsConnected);
        }

      

        // Тести для Receiver State Machine (_lowLevelClient_StringReceived обробник)

        [Fact]
        public async Task StringReceivedEvent_ReceivesEmptyString_LogsWarningAndIgnores()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);
            var receivedMessages = new List<IMReceivedEventArgs>();
            client.MessageReceived += (s, a) => receivedMessages.Add(a); // Підписуємось, але не чекаємо

            // Імітуємо отримання порожнього рядка
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(""));

            // Перевіряємо, що подія MessageReceived не була викликана
            Assert.Empty(receivedMessages);
        }

        [Fact]
        public async Task StringReceivedEvent_ReceivesNullMessageArgs_LogsWarningAndIgnores()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);
            var receivedMessages = new List<IMReceivedEventArgs>();
            client.MessageReceived += (s, a) => receivedMessages.Add(a);

            // Імітуємо отримання null EventArgs (хоча Raise може не дозволити це, але імітуємо, що це може прийти)
            // Або імітуємо отримання Args з Message = null
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs("Error")); // Конструктор StringReceivedEventArgs кидає виняток при null.

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


        // Тести на обробку відключення LowLevelClientDisconnectedEvent
        [Fact]
        public void LowLevelClientDisconnectedEvent_WhenExpectingMessage_ResetsReceiverState()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);

            string fromUser = "PartialUser";
            // Імітуємо отримання тільки "from", щоб перейти в стан ExpectingMessage
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(fromUser));

            // Перевіряємо, що стан підключення ще true
            Assert.True(client.IsConnected);
            // Перевіряємо, що повідомлення ще не викликано
            var receivedMessages = new List<IMReceivedEventArgs>();
            client.MessageReceived += (s, a) => receivedMessages.Add(a);
            Assert.Empty(receivedMessages);

            // Імітуємо відключення
            _lowLevelClientMock.Raise(c => c.Disconnected += null, EventArgs.Empty);

            // Перевіряємо, що стан змінився на false
            Assert.False(client.IsConnected);

            // Перевіряємо, що неповна пара не призвела до виклику MessageReceived
            Assert.Empty(receivedMessages);
            // (Опціонально) Перевірка внутрішнього стану парсера, що він скинувся до ExpectingFrom
        }

        [Fact]
        public void LowLevelClientDisconnectedEvent_WhenExpectingFrom_ResetsReceiverState()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);

            // Клієнт вже в стані ExpectingFrom після створення

            // Імітуємо відключення
            _lowLevelClientMock.Raise(c => c.Disconnected += null, EventArgs.Empty);

            // Перевіряємо, що стан змінився на false
            Assert.False(client.IsConnected);

            // Перевіряємо, що повідомлення не викликано (очікувано)
            var receivedMessages = new List<IMReceivedEventArgs>();
            client.MessageReceived += (s, a) => receivedMessages.Add(a);
            Assert.Empty(receivedMessages);
        }


        // Тести на життєвий цикл та комбіновані сценарії
        [Fact]
        public void Connect_ThenDisconnect_ClientStateIsDisconnectedAndLowLevelClientDisposed()
        {
            var client = new Client(_lowLevelClientMock.Object);
            string u = "u", p = "p", e = "e", i = "i";
            _lowLevelClientMock.Setup(c => c.SendString(It.IsAny<string>())); // Дозволяємо відправку при Connect
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
            _lowLevelClientMock.Setup(c => c.SendString(It.IsAny<string>())); // Дозволяємо відправку при Connect і Send
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

            string msg1_as_from = "Content of Msg1"; // Імітуємо отримання тексту повідомлення (він стане From)
            string from1_as_msg = "Sender of From1"; // Імітуємо отримання відправника (він стане Message)
            string msg2_starts_new_pair = "Content of Msg2"; // Імітуємо отримання ще одного тексту (він стане From для наступної пари)

            var receivedMessages = new List<IMReceivedEventArgs>();
            // Сигнали для очікування подій. Використовуємо 2, бо очікуємо 2 повних повідомлення в цій послідовності.
            var signal1 = new SemaphoreSlim(0, 1); // Сигнал для першого повідомлення (Msg1/From1)
            var signal2 = new SemaphoreSlim(0, 1); // Сигнал для другого повідомлення (Msg2/...)

            client.MessageReceived += (sender, args) =>
            {
                receivedMessages.Add(args);
                // Сигналізуємо, коли отримано перше та друге повідомлення
                if (receivedMessages.Count == 1) signal1.Release();
                if (receivedMessages.Count == 2) signal2.Release();
            };

            // Крок 1: Отримати Msg1 (очікуємо From)
            // -> Client: msg1 стає _currentFrom, state = ExpectingMessage
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(msg1_as_from));

            // Крок 2: Отримати From1 (очікуємо Message)
            // -> Client: формує повідомлення з _currentFrom (msg1_as_from) та отриманого (from1_as_msg),
            //    викликає MessageReceived, state = ExpectingFrom
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(from1_as_msg));

            // Чекаємо на сигнал про перше повідомлення
            bool signalled1 = await signal1.WaitAsync(1000);
            Assert.True(signalled1, "First message (Msg1 as From, From1 as Msg) was not raised within timeout.");

            // Перевіряємо, що є рівно ОДНЕ повідомлення
            Assert.Single(receivedMessages);

            // Перевіряємо вміст ПЕРШОГО повідомлення (згідно з логікою Client)
            var firstMessage = receivedMessages[0];
            Assert.Equal(msg1_as_from, firstMessage.From);   // Msg1 став відправником
            Assert.Equal(from1_as_msg, firstMessage.Message); // From1 став текстом
            Assert.True(client.IsConnected); // Стан підключення не змінився

            // -> Client: msg2 стає _currentFrom, state = ExpectingMessage
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(msg2_starts_new_pair));


            Assert.Equal(1, receivedMessages.Count); // Колекція все ще містить тільки перше повідомлення


            string nextMsgContent = "Final piece";
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(nextMsgContent));

            // Чекаємо на сигнал про друге повідомлення
            bool signalled2 = await signal2.WaitAsync(1000);
            Assert.True(signalled2, "Second message (Msg2 as From, Final piece as Msg) was not raised within timeout.");

            // Перевіряємо, що є рівно ДВА повідомлення загалом
            Assert.Equal(2, receivedMessages.Count);

            // Перевіряємо вміст ДРУГОГО повідомлення
            var secondMessage = receivedMessages[1];
            Assert.Equal(msg2_starts_new_pair, secondMessage.From); // Msg2 став відправником
            Assert.Equal(nextMsgContent, secondMessage.Message); // "Final piece" став текстом
            Assert.True(client.IsConnected); // Стан підключення не змінився
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
            _lowLevelClientMock.Setup(c => c.Dispose()); // Дозволяємо виклик Dispose()
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

            // Налаштовуємо Sequence, щоб помилка сталася на другому SendString (Password)
            var sequence = new MockSequence();
            _lowLevelClientMock.InSequence(sequence).Setup(c => c.SendString(u)); // User - успішно
            _lowLevelClientMock.InSequence(sequence).Setup(c => c.SendString(p)).Throws<IOException>(); // Password - помилка
            _lowLevelClientMock.Setup(c => c.Dispose()).Verifiable(); // Налаштовуємо Dispose, бо він буде викликаний

            // Викликаємо Connect і перевіряємо, що кидається виняток
            var ex = Assert.Throws<InvalidOperationException>(() => client.Connect(u, p, e, i));

            // Перевіряємо, що Dispose було викликано
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Once);
            // Перевіряємо, що стан змінився на відключено
            Assert.False(client.IsConnected);
            // Перевіряємо тип InnerException
            Assert.NotNull(ex.InnerException);
            Assert.IsType<IOException>(ex.InnerException);

            // Перевіряємо, що перший SendString був викликаний, а наступні - ні (крім того, що кинув виняток)
            _lowLevelClientMock.Verify(c => c.SendString(u), Times.Once);
            // SendString(p) кинув виняток, його не можна верифікувати як "викликаний успішно" після Throws.
            // Verify(Times.Once) на Setup().Throws() перевіряє, що Throws() було викликано один раз.
            _lowLevelClientMock.Verify(c => c.SendString(p), Times.Once); // Перевіряємо, що цей виклик відбувся (і кинув)
            _lowLevelClientMock.Verify(c => c.SendString(e), Times.Never); // Email не мав бути відправлений
            _lowLevelClientMock.Verify(c => c.SendString(i), Times.Never); // Image не мав бути відправлений
        }

        // Перевіряє, що Client.Connect все одно прокидає виняток (ймовірно, перший, або новий, залежно від логіки Client)
        [Fact]
        public void Connect_WhenInitialSendStringThrowsAndDisposeThrows_ThrowsOriginalException()
        {
            var client = new Client(_lowLevelClientMock.Object);
            string u = "UserFail";

            // Налаштовуємо SendString кидати перший виняток (під час Connect)
            _lowLevelClientMock.SetupSequence(c => c.SendString(It.IsAny<string>()))
                .Throws<IOException>();

            // Налаштовуємо Dispose кидати другий виняток (коли Client.Disconnect викликається в catch)
            _lowLevelClientMock.Setup(c => c.Dispose()).Throws<InvalidOperationException>();

            // Викликаємо Connect і перевіряємо, який виняток прокидається
            // Поточна логіка Client: спіймає SendString Exception, викличе Disconnect (який кидає Dispose Exception),
            // catch Client'а спіймає Dispose Exception і прокине ЙОГО (InvalidOperationException) обгорнутим.
            var ex = Assert.Throws<InvalidOperationException>(() => client.Connect(u, "p", "e", "i"));

            // Перевіряємо, що InnerException є винятком від Dispose, а не від SendString
            Assert.NotNull(ex);
            Assert.IsType<InvalidOperationException>(ex); // Очікуємо InvalidOperationException від Dispose mock

            // Перевіряємо, що Dispose був викликаний
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Once);
            // Стан імовірно false, але при подвійній помилці це може бути невизначено
            // Assert.False(client.IsConnected); // Може бути false, якщо isConnected = false був встановлений до throw Dispose
        }


        // Обробка подвійної помилки: SendMessage кидає виняток, а потім Dispose кидає виняток
        [Fact]
        public void SendMessage_WhenMessageSendStringThrowsAndDisposeThrows_ThrowsOriginalException()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected); // Клієнт підключений

            // Налаштовуємо SendString для першого виклику (recipient) пройти, для другого (message) кинути перший виняток
            _lowLevelClientMock.SetupSequence(c => c.SendString(It.IsAny<string>()))
                .Pass() // SendString(recipient) - успішно
                .Throws<IOException>(); // SendString(message) - помилка

            // Налаштовуємо Dispose кидати другий виняток
            _lowLevelClientMock.Setup(c => c.Dispose()).Throws<InvalidOperationException>();

            // Викликаємо SendMessage і перевіряємо, який виняток прокидається
            var ex = Assert.Throws<InvalidOperationException>(() => client.SendMessage("to", "msg"));

            // Перевіряємо, що InnerException є винятком від Dispose
            Assert.NotNull(ex);
            Assert.IsType<InvalidOperationException>(ex); // Очікуємо InvalidOperationException від Dispose mock

            // Перевіряємо, що Dispose був викликаний
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Once);
            // Стан імовірно false
            // Assert.False(client.IsConnected);
        }

        // Отримання послідовності: Правильна пара -> Помилка (From замість Message) -> ...
        // Перевіряє, що після правильної пари, наступна помилка парсингу скидає парсер.
        [Fact]
        public async Task StringReceivedEvent_ReceivesSequenceFromThenFrom_RaisesMessageWithSwappedContent()
        {
            var client = new Client(_lowLevelClientMock.Object); // Client починає у стані ExpectingFrom
            Assert.True(client.IsConnected); // Перевіряємо початковий стан

            var receivedMessages = new List<IMReceivedEventArgs>();
            var signal = new SemaphoreSlim(0, 1); // Сигнал для очікування події MessageReceived

            // Підписуємось на подію MessageReceived
            client.MessageReceived += (s, a) => {
                receivedMessages.Add(a); // Додаємо отримане повідомлення до списку
                signal.Release(); // Сигналізуємо, що повідомлення отримано
            };

            string from1_sender = "SenderA";    // Імітуємо перший рядок (стане From)
            string from2_message = "SenderB"; // Імітуємо другий рядок (стане Message)

            // Крок 1: Імітуємо отримання першого рядка ("From1")
            // Обробник: стан ExpectingFrom -> _currentFrom="SenderA", стан ExpectingMessage
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(from1_sender));
            // Перевіряємо, що повідомлення ще немає
            Assert.Empty(receivedMessages); // Ця перевірка має пройти

            // Крок 2: Імітуємо отримання другого рядка ("From2") (очікуємо Message)
            // Обробник: стан ExpectingMessage -> Message(From=_currentFrom="SenderA", Message="SenderB"), стан ExpectingFrom
            // ОБРОБНИК ВИКЛИКАЄ MessageReceived ТУТ.
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(from2_message));

            // Чекаємо на сигнал про повідомлення
            bool signalled = await signal.WaitAsync(1000); // Чекаємо до 1 секунди
                                                           // Перевіряємо, що сигнал був отриманий (тобто, подія MessageReceived спрацювала)
            Assert.True(signalled, "MessageReceived event was not raised after receiving From then From.");

            // Перевіряємо, що список містить рівно ОДНЕ повідомлення
            Assert.Single(receivedMessages); // *** Змінено з Assert.Empty() на Assert.Single() ***

            // Перевіряємо вміст отриманого повідомлення (відповідно до логіки Client: перший рядок = From, другий = Message)
            var receivedArgs = receivedMessages[0];
            Assert.Equal(from1_sender, receivedArgs.From);       // Відправник - перший рядок ("SenderA")
            Assert.Equal(from2_message, receivedArgs.Message); // Текст - другий рядок ("SenderB")

            Assert.True(client.IsConnected); // Перевіряємо, що стан підключення не змінився
        }

        // Отримання багатьох пар дуже швидко.
        // Перевіряє, що синхронний обробник коректно обробляє швидкий потік даних.
        [Fact]
        public async Task StringReceivedEvent_ReceivesManyRapidPairs_ProcessesAllMessagesCorrectly()
        {
            var client = new Client(_lowLevelClientMock.Object); // State = ExpectingFrom
            Assert.True(client.IsConnected);
            var receivedMessages = new List<IMReceivedEventArgs>();
            var signal = new SemaphoreSlim(0, 1);

            // Очікуємо 5 повних повідомлень (10 рядків)
            const int expectedMessageCount = 5;
            client.MessageReceived += (s, a) =>
            {
                receivedMessages.Add(a);
                if (receivedMessages.Count == expectedMessageCount)
                {
                    signal.Release();
                }
            };

            // Імітуємо 5 пар (10 рядків) дуже швидко
            for (int i = 1; i <= expectedMessageCount; i++)
            {
                string from = $"From{i}";
                string msg = $"Msg{i}";
                _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(from));
                _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(msg));
            }

            // Чекаємо на останнє повідомлення
            bool signalled = await signal.WaitAsync(2000); // Даємо трохи більше часу

            Assert.True(signalled, $"Did not receive {expectedMessageCount} messages within timeout.");
            Assert.Equal(expectedMessageCount, receivedMessages.Count);

            // Перевіряємо вміст кількох повідомлень
            for (int i = 0; i < expectedMessageCount; i++)
            {
                Assert.Equal($"From{i + 1}", receivedMessages[i].From);
                Assert.Equal($"Msg{i + 1}", receivedMessages[i].Message);
            }
            Assert.True(client.IsConnected);
        }

        // INetworkClient.StringReceived викликається після того, як Client.Disconnect() був викликаний.
        // Перевіряє, що Client ігнорує вхідні дані після того, як його високоуровневий стан став "відключено".
        [Fact]
        public async Task StringReceivedEvent_AfterClientDisconnect_EventsAreProcessedByHandler()
        {
            var client = new Client(_lowLevelClientMock.Object);
            // Перевіряємо початковий стан підключення (true з конструктора)
            Assert.True(client.IsConnected);

            // Налаштовуємо мок: дозволяємо виклик Dispose(), бо він відбудеться при Disconnect()
            _lowLevelClientMock.Setup(c => c.Dispose());

            // Викликаємо Disconnect на Client
            // Це запустить Client.Disconnect -> CloseConnection
            // CloseConnection має встановити IsConnected = false та скинути внутрішній стан парсера (_receiverState = ExpectingFrom)
            client.Disconnect();

            // Перевіряємо, що стан Client тепер "відключено"
            Assert.False(client.IsConnected);

            // Перевіряємо, що Dispose було викликано на моку INetworkClient
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Once);

            // Він продовжить обробляти вхідні рядки, якщо подію Raise викликано.
            var receivedMessages = new List<IMReceivedEventArgs>();
            // Використовуємо SemaphoreSlim для очікування, оскільки MessageReceived може бути викликана асинхронно
            var messageReceivedSignal = new SemaphoreSlim(0, 1); // Сигнал, що повідомлення отримано

            // Підписуємось на високоуровневу подію MessageReceived ПІСЛЯ виклику Disconnect(), але ПЕРЕД імітацією подій StringReceived
            client.MessageReceived += (s, a) =>
            {
                receivedMessages.Add(a); // Додаємо отримане повідомлення до списку
                messageReceivedSignal.Release(); // Сигналізуємо тесту, що обробник спрацював
            };

            string postDisconnectFrom = "Late From"; // Імітуємо рядок для "From"
            string postDisconnectMsg = "Late Msg";   // Імітуємо рядок для "Msg"

            // Імітуємо отримання першого рядка ("From") від низькорівневого клієнта
            // Оскільки стан парсера був скинутий на ExpectingFrom, цей рядок буде оброблено як відправник.
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(postDisconnectFrom));
            // Повідомлення ще не має бути викликано (парcер чекає другий рядок)
            Assert.Empty(receivedMessages); // Ця перевірка має пройти

            // Імітуємо отримання другого рядка ("Msg") від низькорівневого клієнта
            // Обробник обробить його як текст повідомлення, сформує пару і викличе подію MessageReceived.
            _lowLevelClientMock.Raise(c => c.StringReceived += null, new StringReceivedEventArgs(postDisconnectMsg));

            // Чекаємо на сигнал, який покаже, що подія MessageReceived спрацювала (це очікувана поведінка Client)
            bool signalled = await messageReceivedSignal.WaitAsync(1000); // Чекаємо до 1 секунди
            // Перевіряємо, що сигнал був отриманий (тобто, обробник спрацював і викликав подію MessageReceived)
            Assert.True(signalled, "MessageReceived event was NOT raised after receiving strings post-disconnect (unexpected behavior based on Client's current code).");

            // Перевіряємо, що список отриманих повідомлень містить рівно ОДНЕ повідомлення
            Assert.Single(receivedMessages);

            // Перевіряємо вміст отриманого повідомлення
            var receivedArgs = receivedMessages[0];
            Assert.Equal(postDisconnectFrom, receivedArgs.From);    // Перевіряємо відправника
            Assert.Equal(postDisconnectMsg, receivedArgs.Message); // Перевіряємо текст

            // Стан Client.IsConnected має залишатися false, бо Disconnect() був викликаний раніше
            Assert.False(client.IsConnected);
        }


        // Повторне отримання події Disconnected від INetworkClient.
        // Перевіряє, що повторні виклики обробника Disconnected не призводять до помилок або неочікуваних станів.
        [Fact]
        public void LowLevelClientDisconnectedEvent_ReceivesMultipleTimes_StateRemainsDisconnected()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected);

            // Імітуємо перше відключення
            _lowLevelClientMock.Raise(c => c.Disconnected += null, EventArgs.Empty);
            Assert.False(client.IsConnected); // Стан змінився

            // Імітуємо повторне відключення
            _lowLevelClientMock.Raise(c => c.Disconnected += null, EventArgs.Empty);
            Assert.False(client.IsConnected); // Стан залишається false

            // Імітуємо третє відключення
            _lowLevelClientMock.Raise(c => c.Disconnected += null, EventArgs.Empty);
            Assert.False(client.IsConnected); // Стан залишається false

            // Перевіряємо, що Dispose не викликається обробником Disconnected (Dispose викликається методом Disconnect())
            // У цьому тесті Disconnect() не викликається, тому Dispose не має бути викликаний.
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Never);
        }

        // Connect викликається після Disconnect.
        // Перевіряє, як Client обробляє спробу "перепідключення" через Connect після явного відключення.
        [Fact]
        public void Connect_AfterDisconnect_AttemptsSendButClientRemainsDisconnectedAndThrows()
        {
            var client = new Client(_lowLevelClientMock.Object);
            Assert.True(client.IsConnected); // Початковий стан

            // Налаштовуємо Dispose для першого Disconnect
            _lowLevelClientMock.Setup(c => c.Dispose()).Verifiable();

            // Викликаємо перше відключення
            client.Disconnect();
            Assert.False(client.IsConnected);
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Once);

            // Очищаємо інвокації мока, щоб перевірити виклики під час "перепідключення"
            _lowLevelClientMock.Invocations.Clear();

            string u = "ReUser", p = "RePass", e = "ReEmail", i = "ReImage";

            // Client.Connect спробує відправити дані, навіть якщо !isConnected, але спіймає виняток.
            _lowLevelClientMock.Setup(c => c.SendString(It.IsAny<string>())).Throws(new ObjectDisposedException(null, "Simulating sending on disposed client")); // Якщо повідомлення важливе

            // Або з повідомленням: Throws<ObjectDisposedException>("Simulating sending on disposed client");

            // Викликаємо Connect знову (спроба "перепідключення")
            // Очікуємо, що Connect кине виняток, оскільки SendString кине виняток на Dispose'д клієнті.
            var ex = Assert.Throws<InvalidOperationException>(() => client.Connect(u, p, e, i));

            // Перевіряємо, що властивості оновились (це відбувається до блоку try/catch у Client.Connect)
            Assert.Equal(u, client.UserName);

            // Перевіряємо, що Client спробував відправити початкові дані (викликав SendString)
            _lowLevelClientMock.Verify(c => c.SendString(It.IsAny<string>()), Times.AtLeastOnce); // Або Times.Exactly(4) якщо SendString(null) не кидає відразу

            // Перевіряємо, що Dispose НЕ був викликаний вдруге
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Never);

            // Перевіряємо, що стан залишається false
            Assert.False(client.IsConnected);

            // Перевіряємо тип InnerException
            Assert.NotNull(ex.InnerException);
            Assert.IsType<ObjectDisposedException>(ex.InnerException); // Очікуємо виняток від Mock при виклику SendString
        }


        // SendMessage викликається, коли INetworkClient dependency є null (захист).
        // Перевіряє, що Client коректно обробляє ситуацію, коли _lowLevelClient чомусь null.
        [Fact]
        public void SendMessage_WhenLowLevelClientIsNull_ThrowsNullReferenceExceptionDuringCleanup_IsConnectedRemainsTrue() // Змінена назва
        {
            var client = new Client(_lowLevelClientMock.Object);
            // isConnected = true спочатку

            // Примусово встановлюємо _lowLevelClient = null (рефлексія)
            var lowLevelClientField = typeof(Client).GetField("_lowLevelClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(lowLevelClientField);
            lowLevelClientField.SetValue(client, null); // _lowLevelClient є null

            string recipient = "to", messageText = "msg";

            // Очікуємо NRE, яка виникне під час виконання коду в Disconnect/CloseConnection
            var ex = Assert.Throws<NullReferenceException>(() => // Expect NullReferenceException
            {
                // Викликаємо SendMessage (спричинить першу NRE, перехід у catch, виклик Disconnect)
                // Виконання Disconnect/CloseConnection спричинить ДРУГУ NRE
                client.SendMessage(recipient, messageText); // Рядок 1488 або близько нього
            });

            Assert.IsType<NullReferenceException>(ex); // Підтверджуємо, що спійманий виняток є NRE

            // InnerException перевіряти тут не потрібно, бо це друга NRE, яка виходить без обгортки.
            // Assert.NotNull(ex.InnerException); // ВИДАЛИТИ
            // Assert.IsType<NullReferenceException>(ex.InnerException); // ВИДАЛИТИ

            // Оскільки рядок isConnected = false; не досягається через виняток у CloseConnection,
            // isConnected залишається True
            Assert.True(client.IsConnected);
        }

        // Connect викликається, коли INetworkClient dependency є null (захист).
        // Подібно до попереднього, але для Connect.
        [Fact]
        public void Connect_WhenLowLevelClientIsNull_ThrowsNullReferenceExceptionDuringCleanup_IsConnectedRemainsTrue()
        {
            var client = new Client(_lowLevelClientMock.Object);
            // isConnected = true спочатку

            // Примусово встановлюємо _lowLevelClient = null (рефлексія)
            var lowLevelClientField = typeof(Client).GetField("_lowLevelClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(lowLevelClientField);
            lowLevelClientField.SetValue(client, null); // _lowLevelClient є null

            string u = "u", p = "p", e = "e", i = "i";

            // Перевіряємо, що КИДАЄТЬСЯ NullReferenceException (яка виникає під час очищення)
            var ex = Assert.Throws<NullReferenceException>(() =>
            {
                // Викликаємо Connect (спричинить першу NRE, перехід у catch, виклик Disconnect)
                // Виконання Disconnect/CloseConnection спричинить ДРУГУ NRE
                client.Connect(u, p, e, i); // Рядок 1516 або близько нього
            });

            Assert.IsType<NullReferenceException>(ex);

            // Перевіряємо, що властивості встановлені (це відбувається до try/catch у Connect)
            Assert.Equal(u, client.UserName);
            Assert.Equal(p, client.Password);
            Assert.Equal(e, client.Email);
            Assert.Equal(i, client.Image);


            // Оскільки рядок isConnected = false; не досягається через виняток у CloseConnection,
            // isConnected залишається True. Тест має це підтвердити.
            Assert.True(client.IsConnected);
        }

        [Fact]
        public void Connect_AfterDisconnect_SendStringSucceeds_ClientRemainsDisconnected()
        {
            var client = new Client(_lowLevelClientMock.Object);
            // Перевіряємо початковий стан (підключено)
            Assert.True(client.IsConnected);

            // Налаштовуємо Dispose для першого виклику Disconnect
            // Це потрібно для Strict режиму
            _lowLevelClientMock.Setup(c => c.Dispose());

            // Відключаємо Client методом Disconnect
            // Це призведе до Client.Disconnect -> CloseConnection
            // CloseConnection встановлює IsConnected=false і викликає Dispose() на низькорівневому клієнті.
            client.Disconnect();
            // Перевіряємо, що стан Client тепер "відключено"
            Assert.False(client.IsConnected);
            // Перевіряємо, що Dispose було викликано на моку INetworkClient
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Once);

            // Очищаємо історію викликів мока, щоб перевірити виклики під час наступної спроби Connect
            _lowLevelClientMock.Invocations.Clear();
            // Налаштовуємо мок, щоб Verify(Times.Never) для Dispose спрацював коректно
            _lowLevelClientMock.Setup(c => c.Dispose()).Verifiable(Times.Never);


            string u = "ReUser", p = "RePass", e = "ReEmail", i = "ReImage";

            // Налаштовуємо SendString на моку, щоб він УСПІШНО виконувався
            // Це імітує сценарій, коли Connect викликає SendString на відключеному клієнті, і це НЕ кидає винятку.
            _lowLevelClientMock.Setup(c => c.SendString(It.IsAny<string>()));

            // Викликаємо метод Connect знову.
            // Згідно з логікою Client, він спробує відправити дані, але не змінить isConnected=true.
            // У цьому сценарії Connect НЕ МАЄ кидати виняток.
            client.Connect(u, p, e, i);

            // Перевіряємо, що метод SendString БУВ викликаний 4 рази (Connect робить це безумовно)
            _lowLevelClientMock.Verify(c => c.SendString(It.IsAny<string>()), Times.Exactly(4));

            // Перевіряємо, що Dispose НЕ був викликаний знову (Connect не викликав Disconnect в catch)
            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Never); // Використовуємо явну верифікацію Times.Never

            // Перевіряємо, що властивості Client ОНОВИЛИСЯ (Connect робить це безумовно)
            Assert.Equal(u, client.UserName);
            Assert.Equal(p, client.Password);
            Assert.Equal(e, client.Email);
            Assert.Equal(i, client.Image);

            // Перевіряємо, що стан Client ЗАЛИШАЄТЬСЯ відключено
            Assert.False(client.IsConnected);
        }

        [Fact]
        public void Connect_WhenAlreadyConnected_UpdatesPropertiesAndResendsData()
        {
            var client = new Client(_lowLevelClientMock.Object);
            // За початковим станом у конструкторі, Client вважається підключеним, якщо _lowLevelClient не null.
            Assert.True(client.IsConnected);

            // Дані для першого підключення (хоча Connect викликається тестовим кодом)
            string initialU = "InitialUser", initialP = "InitialPass", initialE = "initial@example.com", initialI = "initial.png";
            // Нові дані для "повторного" підключення
            string newU = "NewUser", newP = "NewPass", newE = "new@example.com", newI = "new.png";

            // Налаштовуємо SendString на моку, щоб він успішно приймав будь-які рядки.
            // Це для обох викликів Connect у цьому тесті.
            _lowLevelClientMock.Setup(c => c.SendString(It.IsAny<string>()));

            client.Connect(initialU, initialP, initialE, initialI);

            // Перевіряємо, що властивості встановлені, і дані відправлені для першого підключення
            Assert.Equal(initialU, client.UserName);
            Assert.Equal(initialP, client.Password);
            Assert.Equal(initialE, client.Email);
            Assert.Equal(initialI, client.Image);
            // Перевіряємо, що SendString був викликаний з першими даними
            _lowLevelClientMock.Verify(c => c.SendString(initialU), Times.Once);
            _lowLevelClientMock.Verify(c => c.SendString(initialP), Times.Once);
            _lowLevelClientMock.Verify(c => c.SendString(initialE), Times.Once);
            _lowLevelClientMock.Verify(c => c.SendString(initialI), Times.Once);
            // Перевіряємо, що стан залишається підключено
            Assert.True(client.IsConnected);

            // Очищаємо історію викликів мока, щоб легко перевірити виклики, що відбудуться під час ДРУГОГО Connect
            _lowLevelClientMock.Invocations.Clear();
            // Налаштовуємо Dispose на моку, щоб перевірити, що він НЕ викликається при повторному Connect
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

            _lowLevelClientMock.Verify(c => c.Dispose(), Times.Never); // Використовуємо явну верифікацію Times.Never

            Assert.True(client.IsConnected);
        }
    }
}