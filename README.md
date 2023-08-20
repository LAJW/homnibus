# Homnibus
Agile Cycle Time Calculator

# Usage

## Read from input file, write to output file

```powershell
./Homnibus input.csv output.csv
```

Errors will be printed to the screen. One-off errors aren't treated as fatal.

## Pipe-style

For combining with other tools in scripts

```powershell
Get-Content input.csv | ./Homnibus > output.csv

Get-Content input.csv | ./Homnibus | My-Other-Tool
```
