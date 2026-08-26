<!-- Thank you for your pull request!  -->
<!-- Please start by describing your change in a few sentences. -->
<!-- You can erase any parts of this template not applicable to your Pull Request. -->

<!--
  ABOUT THE TITLE OF THIS PULL REQUEST — this note is a comment and will not appear
  in the submitted description, so there is nothing to delete.

  The title becomes the commit message on `main`, and release-please reads it to decide
  the next version and write the changelog. It must be a Conventional Commit
  (https://www.conventionalcommits.org/); the `pr-title` workflow rejects anything else.

    feat: …                                   minor bump, listed under Features
    fix: …                                    patch bump, listed under Bug Fixes
    feat!: … or a BREAKING CHANGE: footer     major bump
    build: chore: ci: docs: perf: refactor:
    revert: style: test:                      no release, left out of the changelog

  Describe the change in the title as you want it to read in the release notes.

  Do NOT edit CHANGELOG.md — release-please generates it from commit messages.

  If this pull request is merged WITHOUT squashing, the individual commit messages are
  what release-please reads instead, and those are not checked. Write them the same way.
-->

### What type of change is this?

- [ ] Bug fix in a **backwards-compatible** manner.
- [ ] New feature in a **backwards-compatible** manner.
- [ ] Breaking change: bug fix or new feature that involve incompatible API changes.
- [ ] Other (e.g. doc update, configuration, etc)

### Checklist

_Put an `x` in the boxes that apply. You can also fill these out after creating the PR. If you're unsure about any of them, don't hesitate to ask. We're here to help! This is simply a reminder of what we are going to look for before merging your code._

- [ ] The pull request title is a Conventional Commit, and says what a reader of the release notes needs to know.
- [ ] I ran `dotnet csharpier format .` (CI fails on any deviation).
- [ ] I ran all tests on my computer and it's all green (i.e. `dotnet test`), on both `net9.0` and `net48`.
- [ ] I have added tests that prove my fix is effective or that my feature works.
- [ ] I have added necessary documentation (if appropriate).
- [ ] If this changes the wire format or the public API, I checked it against the
      [runtime contract](https://compas.dev/compas_pb/latest/implementing-a-runtime/)
      and the authoritative Python implementation.
