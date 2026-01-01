using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Api.Astrorise.Argames.DemoGame;
using NetworkFramework.Core.Manager;
using NetworkFramework.Core.Model;
using NetworkFramework.Global;
using NetworkFramework.Interceptor;
using NetworkFramework.Tasks;
using NetworkFramework.Utils;
using Scommon;
using UnityEngine;
using UnityEngine.UI;
using TestEchoReq = Api.Astrorise.Argames.DemoComm.TestEchoReq;
using TestEchoRsp = Api.Astrorise.Argames.DemoComm.TestEchoRsp;


namespace NetworkFramework.Examples
{
    public class UIManager : MonoBehaviour
    {
        // private string _pubKey = "MIIBCgKCAQEAzxXzfImLyQDcISsHusGc35D3GfESlMZzaXZS7WXd9SvSlGaOhbO7" +
        //                          "2+PtLQZiUOw1SHbIrqA+Y9MEOBXTVosrB/CZUKNTGl3EQClJXEUAEVGvzTNdTn0O" +
        //                          "HAy/ww3vgNZ35ll0ZCf5BFgwHhNbDsnPJTXn/nJHg0R+YjctD8jRdFeX79lspqp3" +
        //                          "yAHDDl2fogfuf6CwXst3bkkbzFByDO6sD9P/axSZipn/BF+LtuQeqlwJpl6UxeUC" +
        //                          "+BIRNrkCnIt0dc/d7hgOdzSxRKZm+UWRnlNldpIy85YnZPpO8G9CyiP7UceMdoJD" +
        //                          "5VVQX3aPQcxKQpNs8sQV5iDgoAn96huXpQIDAQAB";
        
        private string _pubKey = "MIGJAoGBAMemgJuzFBXPZCmYRWR1k9iFHMfOcorItJJ0d7AWnUW88cjJwOjN4Y/uxiu6UU7i5J5or7jACY7yHIwVEdUC2PcxDyFoaN6UoZyydhaC3Sx10Ltkr6yuquZopNQy1/rzfdYAlU2STyhHFMFuuHOdDsViTDqgDYKWdzANH3ebqoCZAgMBAAE=";
        

        private string _pubKeyNo = "0";

        private string _token = "";

        // astrorise
        // private string _inconstantConnectionUrl = "http://dev-api.ruok.live/sgw/api";
        // private string _inconstantConnectionUrl = "http://dev-api.ruok.live/sgw/api?app=demo&debug-uid=100012321000";
        private string _inconstantConnectionUrl = "http://test-api.pindef.net/sgw/api?debug-uid=100012321000";

        // private string _persistentConnectionUrl = "ws://dev-api.ruok.live/sgw/ws";
        // private string _persistentConnectionUrl = "ws://dev-api.ruok.live/sgw/ws?app=demo&debug-uid=100012321000";
        private string _persistentConnectionUrl = "ws://test-api.pindef.net/sgw/ws?to-biz=demo-game&debug-uid=100012321000";


        public Button httpButton;
        public Button webSocketConnectButton;
        public Button joinGroupButton;
        public Button webSocketButton;
        public Button webSocketDisconnectButton;


        private void Start()
        {
            // 初始化对象
            InitObjects();

            // 绑定按钮事件
            httpButton.onClick.AddListener(() => OnHttpButtonClick("httpButton"));
            webSocketConnectButton.onClick.AddListener(() => OnWebSocketConnectButtonClick("webSocketConnectButton"));
            joinGroupButton.onClick.AddListener(() => OnJoinGroupButtonClick("joinGroupButton"));
            webSocketButton.onClick.AddListener(() => OnWebSocketButtonClick("webSocketButton"));
            webSocketDisconnectButton.onClick.AddListener(() => OnWebSocketDisconnectButtonClick("webSocketDisconnectButton"));
        }

        private void InitObjects()
        {
            // 这里可以实例化一些预制体，或者加载数据
            LoggerUtil.Enabled = true;
            GlobalExceptionHandler.Enabled = true;
            NetworkManager.Instance.Init(_inconstantConnectionUrl, _persistentConnectionUrl, _token,
                new AppNetConfig(_pubKeyNo, _pubKey));
            LoggerUtil.Log("Object initialization completed!");

            NetworkManager.Instance.RegisterPushHandler($"{_persistentConnectionUrl}", OnChatPush);
        }

        private void OnHttpButtonClick(string buttonName)
        {
            LoggerUtil.Log($"{buttonName} was clicked!");
            OnSayHelloRequest(ConnectionType.INCONSTANT,
                "astrorise.argames.demo", "Common.TestEcho",
                Serializer.SerializeToProtoBuf(new TestEchoReq()),
                s => { Debug.Log($"success callback : {s}"); },
                s => { Debug.Log($"error callback {s}"); });
        }

        private void OnJoinGroupButtonClick(string buttonName)
        {
            LoggerUtil.Log($"{buttonName} was clicked!");
            OnJoinGroupRequest(ConnectionType.PERSISTENT,
                "astrorise.argames.demo", "Game.TestJoinGGroup",
                Serializer.SerializeToProtoBuf(new TestJoinGGroupReq
                {
                    Group = "10086"
                }),
                s => { Debug.Log($"success callback : {s}"); },
                s => { Debug.Log($"error callback {s}"); });
        }


        private void OnWebSocketConnectButtonClick(string buttonName)
        {
            LoggerUtil.Log($"{buttonName} was clicked!");

            var task = new PriorityTask<Response>(
                name: "FetchData",
                priority: TaskPriority.Normal,
                work: ct => NetworkManager.Instance.Connect(
                    wsUrl: "ws://test-api.pindef.net/sgw/ws?debug-uid=100012321000",
                    token: ct
                )
            );

            NetworkManager.Instance.Scheduler.Enqueue(
                task,
                onSuccess: result =>
                {
                    LoggerUtil.Log(
                        $"Connected result, NetCode: {result?.NetCode.ToString()}, NetMessage : {result?.NetMessage}, Code : {result?.Code}, Message: {result?.Message}");
                },
                onError: ex => { LoggerUtil.LogError($"Failed: {ex.Message}"); },
                onCanceled: () => { Debug.Log($"Canceled"); }
            );

            _lastTask = task;
        }

        private void OnWebSocketButtonClick(string buttonName)
        {
            LoggerUtil.Log($"{buttonName} was clicked!");
            OnNotifyRequest(ConnectionType.PERSISTENT,
                "astrorise.argames.demo", "Game.TestNotify",
                // "demo.main", "Common.TestNotify",
                Serializer.SerializeToProtoBuf(new TestNotifyReq()
                {
                    Group = "10086",
                    Msg = "TestNotify10086"
                }),
                s => { Debug.Log($"success callback : {s}"); },
                s => { Debug.Log($"error callback {s}"); });
        }

        private void OnSayHelloRequest(ConnectionType userConnectionType, string service, string method, byte[] body,
            Action<string> successCallback,
            Action<string> errorCallback)
        {
            var request = new Request
            {
                UseConnectionType = userConnectionType,
                RequestControl = new RequestControl
                {
                    Service = service,
                    // Method = "Common.Heartbeat",
                    Method = method,
                    Reason = RPCReason.UserAction
                },
                // Body = Serializer.SerializeToProtoBuf(new HeartbeatReq())
                Body = body
            };


            var task = new PriorityTask<Response>(
                name: "FetchData",
                priority: TaskPriority.Normal,
                work: ct => NetworkManager.Instance.Request(
                    request: request,
                    token: ct
                )
            );

            NetworkManager.Instance.Scheduler.Enqueue(
                task,
                onSuccess: result =>
                {
                    // Debug.Log($"请求结果: {result}");
                    LoggerUtil.Log(
                        $"Request result, NetCode: {result?.NetCode.ToString()}, NetMessage : {result?.NetMessage}, Code : {result?.Code}, Message: {result?.Message}");
                    var testEchoRsp = Serializer.DeserializeFromProtoBuf<TestEchoRsp>(result?.Body);
                    LoggerUtil.Log(
                        $"testEchoRsp Uid: {testEchoRsp?.Uid}, Msg : {testEchoRsp?.Msg}");
                    successCallback(result?.NetCode.ToString());
                },
                onError: ex =>
                {
                    LoggerUtil.LogError($"Failed: {ex.Message}");
                    errorCallback?.Invoke(ex.Message);
                },
                onCanceled: () => { Debug.Log($"Canceled"); }
            );

            _lastTask = task;
        }

        private void OnJoinGroupRequest(ConnectionType userConnectionType, string service, string method, byte[] body,
            Action<string> successCallback,
            Action<string> errorCallback)
        {
            var request = new Request
            {
                PersistentConnectionUrl = $"{_persistentConnectionUrl}",
                UseConnectionType = userConnectionType,
                RequestControl = new RequestControl
                {
                    Service = service,
                    Method = method,
                    Reason = RPCReason.UserAction
                },
                Body = body
            };


            var task = new PriorityTask<Response>(
                name: "FetchData",
                priority: TaskPriority.Normal,
                work: ct => NetworkManager.Instance.Request(
                    request: request,
                    token: ct
                )
            );

            NetworkManager.Instance.Scheduler.Enqueue(
                task,
                onSuccess: result =>
                {
                    // Debug.Log($"请求结果: {result}");
                    LoggerUtil.Log(
                        $"Request result, NetCode: {result?.NetCode.ToString()}, NetMessage : {result?.NetMessage}, Code : {result?.Code}, Message: {result?.Message}");
                    var testJoinGGroupRsp = Serializer.DeserializeFromProtoBuf<TestJoinGGroupRsp>(result?.Body);
                    LoggerUtil.Log($"testJoinGGroupRsp : {testJoinGGroupRsp}");
                    successCallback(result?.NetCode.ToString());
                },
                onError: ex =>
                {
                    LoggerUtil.LogError($"Failed: {ex.Message}");
                    errorCallback?.Invoke(ex.Message);
                },
                onCanceled: () => { Debug.Log($"Canceled"); }
            );

            _lastTask = task;
        }

        private void OnNotifyRequest(ConnectionType userConnectionType, string service, string method, byte[] body,
            Action<string> successCallback,
            Action<string> errorCallback)
        {
            var request = new Request
            {
                PersistentConnectionUrl = $"{_persistentConnectionUrl}",
                UseConnectionType = userConnectionType,
                RequestControl = new RequestControl
                {
                    Service = service,
                    // Method = "Common.Heartbeat",
                    Method = method,
                    Reason = RPCReason.UserAction
                },
                // Body = Serializer.SerializeToProtoBuf(new HeartbeatReq())
                Body = body
            };


            var task = new PriorityTask<Response>(
                name: "FetchData",
                priority: TaskPriority.Normal,
                work: ct => NetworkManager.Instance.Request(
                    request: request,
                    token: ct
                )
            );

            NetworkManager.Instance.Scheduler.Enqueue(
                task,
                onSuccess: result =>
                {
                    // Debug.Log($"请求结果: {result}");
                    LoggerUtil.Log(
                        $"Request result, NetCode: {result?.NetCode.ToString()}, NetMessage : {result?.NetMessage}, Code : {result?.Code}, Message: {result?.Message}");
                    var testNotifyRsp = Serializer.DeserializeFromProtoBuf<TestNotifyRsp>(result?.Body);
                    LoggerUtil.Log($"testNotifyRsp : {testNotifyRsp}");
                    successCallback(result?.NetCode.ToString());
                },
                onError: ex =>
                {
                    LoggerUtil.LogError($"Failed: {ex.Message}");
                    errorCallback?.Invoke(ex.Message);
                },
                onCanceled: () => { Debug.Log($"Canceled"); }
            );

            _lastTask = task;
        }

        private void OnWebSocketDisconnectButtonClick(string buttonName)
        {
            LoggerUtil.Log($"{buttonName} was clicked!");
            NetworkManager.Instance.Disconnect(wsUrl: "ws://test-api.pindef.net/sgw/ws?debug-uid=100012321000");
        }

        private PriorityTask<Response> _lastTask;
        void CancelActive() => _lastTask?.Cancel();


        // 推送处理函数
        private void OnChatPush(PushMessage msg)
        {
            LoggerUtil.Log(
                $"[Push] url={msg.Url}, pkg={msg.NotifyPkg}, type={msg.ContentType}, gzip={msg.CompressType}");

            // 业务解包示例：按 ContentType 选择反序列化
            // 根据 NotifyPkg 判断具体的PB类型
            if (msg.NotifyPkg == "api.astrorise.argames.demo_game.NotifySync1")
            {
                NotifySync1 notify = null;
                if (msg.ContentType == ContentType.Proto)
                {
                    notify = Serializer.DeserializeFromProtoBuf<NotifySync1>(msg.Body);
                }
                else
                {
                    var json = Encoding.UTF8.GetString(msg.Body);
                    notify = Serializer.DeserializeFromJson<NotifySync1>(json);
                }

                if (notify == null)
                {
                    LoggerUtil.LogError("[Push] Deserialize NotifySync1 failed");
                    return;
                }

                // 示例：输出核心字段
                LoggerUtil.Log($"[NotifySync1] msg={notify.Msg}, bCast={notify.BCast}");
            }
            else if (msg.NotifyPkg == "api.astrorise.argames.demo_game.NotifySync2")
            {
                NotifySync2 notify = null;
                if (msg.ContentType == ContentType.Proto)
                {
                    notify = Serializer.DeserializeFromProtoBuf<NotifySync2>(msg.Body);
                }
                else
                {
                    var json = Encoding.UTF8.GetString(msg.Body);
                    notify = Serializer.DeserializeFromJson<NotifySync2>(json);
                }

                if (notify == null)
                {
                    LoggerUtil.LogError("[Push] Deserialize NotifySync2 failed");
                    return;
                }

                // 示例：输出核心字段
                LoggerUtil.Log($"[NotifySync2] msg={notify.Msg}, bCast={notify.BCast}");
            }
            else if (msg.NotifyPkg == "api.astrorise.argames.demo_game.NotifySync3")
            {
                NotifySync3 notify = null;
                if (msg.ContentType == ContentType.Proto)
                {
                    notify = Serializer.DeserializeFromProtoBuf<NotifySync3>(msg.Body);
                }
                else
                {
                    var json = Encoding.UTF8.GetString(msg.Body);
                    notify = Serializer.DeserializeFromJson<NotifySync3>(json);
                }

                if (notify == null)
                {
                    LoggerUtil.LogError("[Push] Deserialize NotifySync3 failed");
                    return;
                }

                // 示例：输出核心字段
                LoggerUtil.Log($"[NotifySync3] msg={notify.Msg}, bCast={notify.BCast}");
            }
            else
            {
                LoggerUtil.LogWarning($"[Push] Unhandled NotifyPkg: {msg.NotifyPkg}");
            }
        }
    }
}