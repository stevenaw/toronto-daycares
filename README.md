# Toronto Daycares
A rudimentary, not-always-end-user-friendly command line tool to help find high-rated nearby daycares in Toronto.
Results will be grouped by child care program and ordered by ranking.

## Result Limiting
Toronto has a lot of city-registered daycares. To limit the number of results, use the `-n` argument. For example, to return the top 50
results:

```
-n 50
```

## Search Modes
The below search mode can be combined as desired. For example, one may want to search by both location and program to find the nearest daycares
offering infant and toddler programs:

```
--wards 6,8,11,18,17,16,15  --programs Infant,Toddler
```

### Search by Childcare Program
A comma-delimited list of programs can be provided to filter results by program offering:

```
--programs Infant,Toddler
```

Valid options are:
- Infant,
- Toddler,
- Preschool,
- Kindergarten

### Search by Location
Searching by location can be done by city ward or by address proximity. As the city tracks its own daycare information by ward number, this
is the most reliable option. Searching by addresss proximity is fairly reliable, but some data entered within the city's website can cause
issues with GPS resolution. Searching for daycares by addresses uses [OpenStreetMaps](https://operations.osmfoundation.org/policies/nominatim/)
for resolving an address to GPS coordinates, and is compliant with all required caching and rate limiting recommendations for their service.

#### Search by City Ward
To search for daycares by ward number, simply provide a comma-delimited list of ward numbers to the CLI. For example:

```
--wards 6,8,11,18,17,16,15
```

For a map of city wards, please see the [city's website](https://www.toronto.ca/city-government/data-research-maps/neighbourhoods-communities/ward-profiles/).

#### Search by Address Proximity

The `--address` option can be used to search for daycares by proximity to an address. This will filter to the closest n results. Proximity
is calculated using the greater-circle distance between GPS coordinates.

```
--address "5100 Yonge St"
```

## Output
Results can be output to the command line or to an excel file. The default is to command line.
To output results to an Excel file, provide the file name using the `--output` argument.

```
--output MyFile.xlsx
```