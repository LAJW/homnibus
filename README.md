# Homnibus

Agile Cycle Time Calculator

Not to be confused with the Homnibus from The Smurfs:

<img src="./Homnibus.jpg" alt="An image of Homnibus from The Smurfs">

Gives you:
- Total time worked on a ticket (rounded up to a day)
- Time since the first time the ticket was started being worked on
- Time since the last time the ticket was started being worked on
- Number of process violations
- Number of times the ticket was pushed back against the flow of the process
- Number of times the ticket skipped over steps in the process

## Configuration

Process is defined in the configuration (for now hard-coded in Homnibus/Program.fs). Yaml-based configuration is coming
soon™.

## Usage

### Read from input file, write to output file

```powershell
./Homnibus input.csv output.csv
```

Parsing errors will be printed to the screen. One-off errors aren't treated as fatal. Unability to parse any lines is
treated as fatal.

### Pipe-style

For combining with other tools in powershell scripts

```powershell
Get-Content input.csv | ./Homnibus > output.csv

Get-Content input.csv | ./Homnibus | My-Other-Tool
```
