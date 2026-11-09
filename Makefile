# Documentation helpers for this package (docs.omilaxr.dev)
# Usage: make <target>
# Notes:
# - The logic lives in the docs repo (OmiLAXR-Ecosystem/docs), checked out next to this package
# - Pages you write go into Documentation~/; component pages are generated from the source code

DOCS ?= ../docs

.DEFAULT_GOAL := help

.PHONY: help docs docs-check docs-page docs-component docs-publish docs-repo

help: ## Show available commands
	@grep -E '^[a-zA-Z_-]+:.*?## ' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-15s\033[0m %s\n", $$1, $$2}'

docs-repo:
	@test -f $(DOCS)/scripts/packages.mjs || (echo "Docs repo not found at $(DOCS). Clone git@github.com:OmiLAXR-Ecosystem/docs.git next to this package or pass DOCS=<path>."; exit 1)

docs: docs-repo ## Preview the docs site built from the repos on your disk
	$(MAKE) -C $(DOCS) local dev WORKSPACE=$(abspath ..)

docs-check: docs-repo ## List components of this package without a /// <summary>
	$(MAKE) -C $(DOCS) check PKG=$(CURDIR)

docs-page: docs-repo ## Create Documentation~/<NAME>.md, e.g. make docs-page NAME=guides/setup
	$(MAKE) -C $(DOCS) new-page PKG=$(CURDIR) NAME=$(NAME)

docs-component: docs-repo ## Add text to a component page, e.g. make docs-component NAME=StatementPrinter
	$(MAKE) -C $(DOCS) new-component PKG=$(CURDIR) NAME=$(NAME)

docs-publish: docs-repo ## Rebuild docs.omilaxr.dev now (needs the GitHub CLI)
	$(MAKE) -C $(DOCS) publish
