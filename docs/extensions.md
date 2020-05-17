# Extensions

Extensions mechanism allows creating complex user scenarios, especially in case of customized setups or permissioned networks.

## Extension developer's guide:

- Create new NetCore 3.1 library project in VisualStudio.
- Add references to Centaurus.Domain and Centaurus.Common projects.
- Implement `IExtension` interface from Centaurus.Domain project.
- Subscribe to one or more [events](#extension-points) of `Global.ExtensionsManager` instance.
- Create an extension [configuration file](#config-file)  and place the extension dll file to the same folder.
- Specify the path to the extension config file using `--extensions_config_file_path path-to-config` param while starting Alpha and auditors.

## Config file

### Config is a json file with only one (for now) property:

- **`extensions`** (`Array<ExtensionItem>`) - Array of extension items.

### Each extension item consists of: 

- **`name`** (`string`) - This is dll name without `dll` extension. The extension engine will look for a library with this name.
- **`isDisabled`** (`bool`) - You can disable extension by setting this property to `true`. Default value is `false`.
- **`extensionConfig`** (`object`) - Config for the extension – plain string key-value pairs. This object will be deserialized as `Dictionary<string, string>` and passed to `IExtension.Init` method.

## Extension points

All events are executed synchronously within client connection (not blocking other connections or affecting quantum processing), except `OnBeforeQuantumHandle` and `OnAfterQuantumHandle` – these two events are executed synchronously in the context of the entire quantum processing pipeline.

Events                   | Arguments                                                                              
-------------------------|-----------
`OnNewConnection`        | `WebSocket` webSocket, `string` ip                                                    
`OnConnectionValidated`  | `BaseWebSocketConnection` connection                                                   
`OnBeforeValidateMessage`| `BaseWebSocketConnection` connection, `MessageEnvelope` message                        
`OnAfterValidateMessage` | `BaseWebSocketConnection` connection, `MessageEnvelope` message                        
`OnBeforeHandleMessage`  | `BaseWebSocketConnection` connection, `MessageEnvelope` message                        
`OnAfterHandleMessage`   | `BaseWebSocketConnection` connection, `MessageEnvelope` message                        
`OnHandleMessageFailed`  | `BaseWebSocketConnection` connection, `MessageEnvelope` message, `Exception` exception 
`OnBeforeSendMessage`    | `BaseWebSocketConnection` connection, `MessageEnvelope` message                        
`OnAfterSendMessage`     | `BaseWebSocketConnection` connection, `MessageEnvelope` message                        
`OnSendMessageFailed`    | `BaseWebSocketConnection` connection, `MessageEnvelope` message, `Exception` exception 
`OnBeforeConnectionClose`| `BaseWebSocketConnection` connection                                                   
`OnBeforeNotify`         | `RawPubKey` pubKey, `MessageEnvelope` envelope                                         
`OnBeforeNotifyAuditors` | `MessageEnvelope` envelope                                                             
`OnBeforeQuantumHandle`  | `MessageEnvelope` quantum                                                              
`OnAfterQuantumHandle`   | `ResultMessage` resultMessage                              
