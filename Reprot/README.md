# LaTeX Template for MMP Report

This folder contains a LaTeX template for the project report. 

In a change to previous years, there is one combined template for the Engineering-oriented and Research-oriented projects. The organisation of the files has been simplified to make it easier to maintain this resource. 

## Engineering or Research

Within `mmp-report.tex`, there is a defintion for `\reporttype`. This can have one of two values: `Engineering` or `Research`. This value is used to determine which of the two `Chapters_x` folders are read, where x is one of `Engineering` or `Research`.

## Colours

The title, chapter and section headings are assigned a blue colour within the template. To control that colour and, if you prefer, remove the colour to simply use the default font colour, you can adjust the setting in `mmp-report.tex`.  Look for the comments on `Title and Section Colours`.

## Style File

The LaTeX style file is defined in  `StylesAndReferences/mmp-report.sty`.  Editing this file is for those who are advanced users of LaTeX or the curious who are willing to learn something new. One area you may want to modify is the default choice of font. The document is setup to use `tgheros`, a sans serif font. You may prefer a different font or even a serif font. Look for the `% Fonts` comment in the style file to find the elements to change. 

## Bibliography / References

The set of example references is contained in the file `StylesAndReferences/references.bib`. This has been manually created but it could easily be something that is generated from a reference management tool such as Mendeley. 

Two example reference styles are provided for in the `mmp-report.tex` file. One is an author-date style (`StylesAndReferences/authordate2annot`) and one is an IEEE style (`StylesAndReferences/IEEEannotU`). 

## Source of help for LaTeX

The internet is not short of good resources to help you get the most out of LaTeX. One useful resource is hosted by [Overleaf](https://www.overleaf.com/learn/latex/Main_Page), which is also an online LaTeX editor. 

A LaTeX channel is also available on the Slack workspace that has been setup for the module. If you have questions about this template, then please ask there and possibly share your experience. To find out how to access the Slack workspace, see the home page for the module in Bb. 


Neil Taylor
19th February 2019
