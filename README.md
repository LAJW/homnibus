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

## Building

- Requires [dotnet core 7.0](https://dotnet.microsoft.com/en-us/download/dotnet/7.0), which you can download from
  Microsoft's website.

In terminal:
```powershell
cd homnibus/Homnibus
dotnet build
```

The binary will appear in the `homnibus/Homnibus/bin` directory.

## Configuration

Process is defined in the configuration (for now hard-coded in Homnibus/Program.fs). Yaml-based configuration is coming
soon™.

## Usage

### Read from input file, write to output file

After just compiled with dotnet core
```bash
dotnet run input.csv
```

or (when using the binary directly)
```bash
./Homnibus input.csv output.csv
```

Parsing errors will be printed to the screen. One-off errors aren't treated as fatal. Unability to parse any lines is
treated as fatal.

### Pipe-style

For combining with other tools in powershell scripts

```bash
cat input.csv | ./Homnibus > output.csv
```

Or, if you wish to pipe the output to `my-custom-tool`
```bash
cat input.csv | ./Homnibus | my-custom-tool
```
