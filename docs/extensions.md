# Extensions

Centaurus supports extensions. 
To create new extension you need to start new library project and add references to Centaurus.Domain and Centaurus.Common projects. 
Then you need to implement IExtension interface from Centaurus.Domain project. After that you can subscribe to events of Global.ExtensionsManager instance. 
Create extensions configurable file and place your extension dll file to the same folder. Specify path to the extension config file for Alpha or auditor. To do that, run executable with param `--extensions_config_file_path path-to-config`

### Config example
```
{
	"extensions": [
		{
			name: "SomeExtension", //extension name
            isDisabled: false, //false is default value
			extensionConfig: { //this is where your extension settings should be placed. It will be passed to extension as a dictionary
				prop1: 1,
				prop2: false,
				prop3: "test"
			}
		}
	]
}
```