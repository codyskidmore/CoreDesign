#!/bin/bash
# Find and remove all bin directories
find . -type d -name "bin" -exec rm -rf {} \; 2>/dev/null || true
# Find and remove all obj directories
find . -type d -name "obj" -exec rm -rf {} \; 2>/dev/null || true