# CRealize

CRealize is a non-invasive C# object serialization framework with JSON support.

## Features

 - Can handle any class or struct out of the box, no attributes or converters required
 - All writable public properties and fields are recursively serialized and deserialized
 - Polymorphic types are fully supported and automatically handled
 - Permissive deserialization: missing values != fatal failures
 - Optional pretty JSON formatting (lifted from StackOverflow, because of course)

## Limitations

- CRealize lacks some of the advanced customizations that heavier frameworks tend to offer, e.g. type converters
- Performance, while not downright poor, isn't the primary goal of the project; optimization opportunities remain

## How to use

```C#
// Serialization
string str = CRealize.Convert.ToJSON(myObjectIn);

// Deserialization
MyType myObjectOut = CRealize.Convert.FromJSON<MyType>(str);
```

CRealize tries not to throw exceptions, so you should null-check the output.

## Going forward

A few features are planned for future releases:

 - Support more common formats out of the box: XML, binary etc.
 - Support user-defined format plugins
 - Optimize the reflection logic by caching some of the reflected type information

## License

CRealize is provided under the MIT license. See the LICENSE file for legalese.
