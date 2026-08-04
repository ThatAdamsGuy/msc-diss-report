# Dissertation Worklog



## Pass 2 - Problem Definition

- Replaced placeholder structure with dissertation scaffold.
- Split Deliverables from Objectives.
- Added Project Scope section.
- Split Assumptions and Limitations into separate sections.
- Emphasised software is a means to evaluate the engineering model.
- Added guidance to keep equations and implementation details out of this chapter.
- Added suggested objective list and project flow figure.

## Pass 3 - Model Development
- Expanded Chapter 3 into a progressive engineering narrative.
- Added modelling philosophy before equations.
- Added comparison window section.
- Added scenario simulation section.
- Added model assumptions and limitations sections.
- Added model verification roadmap.
- Intentionally structured model from simple to complex.
- Recommendation: if page limit becomes an issue, implement minimum model first; leave advanced extensions as future work rather than deleting the discussion.


## Pass 4
- Aggressively restructured Chapter 4 around implementation philosophy rather than UI.
- Added Software Architecture and Data Sources sections.
- Reframed Race Trace as engineering visualisation.
- Added explicit lower-priority markers.


## Pass 5
- Reframed Chapter 5 around engineering validation rather than software testing.
- Reframed Chapter 6 around interpretation of results and engineering insight.
- Tightened conclusion structure.
- Priority reminder: If time constrained, prioritise sensitivity analysis and discussion over extra implementation features.


## Pass 6
- Restructured Results & Evaluation scaffold to focus on validation, sensitivity analysis and engineering discussion.
- Explicitly separated discussion, limitations and future work.
- Marked sensitivity analysis as highest priority section.


## Pass 7
- Expanded Conclusion into objective-driven closure.
- Added Research Contributions section.
- Added Engineering Reflection separate from Future Work.
- Future work prioritised.


## Pass 8 - Whole-dissertation structural review

- Reviewed the complete project source tree rather than a single chapter.
- Reframed Chapter 1 as background plus an integrated research-context/literature section; moved aim/objectives/scope wholly to Chapter 2 to remove duplication.
- Added problem statement, research questions, measurable success criteria, revised project planning and risk management to Chapter 2.
- Rebuilt Chapter 3 around notation, minimum model, extensions, scenario definition, comparison window, pit-loss symmetry, cumulative formulation, outputs, provenance and a worked example.
- Rebuilt Chapter 4 around requirements, final architecture, data preparation, model-engine mapping, race trace, software verification and analytical development decisions.
- Separated Chapter 5 methodology from Chapter 6 results. Chapter 5 now defines software/model verification, case selection, validation metrics, sensitivity, uncertainty and bias controls. Chapter 6 now reports and interprets those results.
- Consolidated future work into Chapter 7 and removed avoidable duplication with Chapter 6.
- Created Appendices C--F because the main file already included them: parameter dictionary, software verification evidence, validation data/case notes, and reproduction/user guide.
- Added `DISSERTATION_PLAN.md` containing minimum-complete scope, priorities, required figures/tables and final checks.
- Cleaned major LaTeX structural issues in `mmp-report.tex`: duplicate column type, duplicate list-of-equations definitions, blanket `\nocite{*}`, and bibliography title.
- Flagged unresolved source consistency: FastF1 versus OpenF1 must match final implementation, references and licensing appendix.

- Reviewed front matter, bibliography scaffolding, appendices, build file and archived chapters.
- Added explicit final checks for title-page date/version/email and declaration consent/date.
- Added reference-database requirements without inventing sources.
- Added an archive warning for `OldChapters/`.
- Confirmed the source compiles with `pdflatex` after structural changes (bibliography generation could not be tested in this environment because the `bibtex` executable is unavailable).


## Pass 9
Added DISSERTATION_DESIGN.md containing thesis statement, argument map, figure/table checklist, literature matrix placeholder and examiner questions.
