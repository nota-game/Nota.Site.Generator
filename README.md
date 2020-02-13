# Nota.Site.Generator

Still in early development.

Try out the [Sample project](https://github.com/nota-game/Nota.Site.Generator.Test/tree/master) to build with this tool. You can execute the tool in the root of the project

## Features

- **Support for incremental generation**  
  If an input changed, output documents are only regenerated if that change will affact the output.
- **Versioning of content**  
  The generator uses git metadata to generate all the (taged) Versions of the content. Not only the latest.
  This way older versions are still accassable.


### Markdown Syntax

*ToDo: define the deiferences to commonMark*

An input markdown will not automaticly generate an output html. Multiple input files will be merged together.
For every chapter (header level 1) a document will be generated.

#### Not supported
- Underline Style headder
- Quotes
- links other then Markdown links
- html

#### New Elements

##### Aside

    | {ID}
    | {type}
    | {tag_0}
    | {tag_n}
    |-------------
    | Content
    | of the block

Generates an aside element `id` is used to reference this element. `type` is one of `{Sample, Information, Optional, GameMaster}`
`tag_i` is a key followed by a number. It is used to define who has access to this information.

##### Orderung
A document may start with

```
---
after:  {id}
---
```

This describtes the order of the document relativ to its presuccessor. It is used to merge the doucuments together to chapters

##### Include

`~[{ID}]`

This will be replaced by the document referenced by `ID`
