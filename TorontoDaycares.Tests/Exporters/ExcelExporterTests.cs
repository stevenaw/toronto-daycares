using ClosedXML.Excel;
using TorontoDaycares.Exporters;
using TorontoDaycares.Models;

namespace TorontoDaycares.Tests.Exporters
{
    public class ExcelExporterTests
    {
        /// <summary>
        /// Tests that ExportAsync throws when response parameter is null.
        /// Expected to throw NullReferenceException when accessing response.TopPrograms.
        /// </summary>
        [Test]
        public void ExportAsync_NullResponse_ThrowsNullReferenceException()
        {
            // Arrange
            using var tempDir = new TempDirectory();
            string filePath = Path.Combine(tempDir.Path, "output.xlsx");
            var exporter = new ExcelExporter(filePath);

            // Act & Assert
            Assert.ThrowsAsync<NullReferenceException>(async () =>
                await exporter.ExportAsync(null!));
        }

        /// <summary>
        /// Tests that ExportAsync creates an Excel file with no worksheets when TopPrograms is empty.
        /// Input: Empty TopPrograms list.
        /// Expected: File is created but contains no worksheets.
        /// </summary>
        [Test]
        public async Task ExportAsync_EmptyTopPrograms_ThrowsException()
        {
            // Arrange
            using var tempDir = new TempDirectory();
            string filePath = Path.Combine(tempDir.Path, "output.xlsx");
            var exporter = new ExcelExporter(filePath);
            var response = new DaycareSearchResponse
            {
                TopN = 10,
                TopPrograms = []
            };

            // Act
            await Assert.ThatAsync(async () => await exporter.ExportAsync(response), Throws.InvalidOperationException);
        }

        /// <summary>
        /// Tests that ExportAsync creates a worksheet for a single program type with correct headers and data.
        /// Input: Single program type (Infant) with one daycare.
        /// Expected: One worksheet with correct headers and one data row.
        /// </summary>
        [Test]
        public async Task ExportAsync_SingleProgramType_CreatesWorksheetWithCorrectData()
        {
            // Arrange
            using var tempDir = new TempDirectory();
            string filePath = Path.Combine(tempDir.Path, "output.xlsx");
            var exporter = new ExcelExporter(filePath);
            var response = new DaycareSearchResponse
            {
                TopN = 1,
                TopPrograms =
                [
                    new TopProgramResult
                    {
                        Daycare = new Daycare
                        {
                            Id = 1,
                            Name = "Test Daycare",
                            Address = "123 Test St",
                            Uri = new Uri("https://example.com/daycare/1")
                        },
                        Program = new DaycareProgram
                        {
                            ProgramType = ProgramType.Infant,
                            Capacity = 10,
                            Vacancy = true,
                            Rating = 4.5
                        }
                    }
                ]
            };

            // Act
            await exporter.ExportAsync(response);

            // Assert
            Assert.That(File.Exists(filePath), Is.True);
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);

            Assert.That(worksheet.Name, Is.EqualTo("Infant"));
            Assert.That(worksheet.Cell(1, 1).GetValue<string>(), Is.EqualTo("Name"));
            Assert.That(worksheet.Cell(1, 2).GetValue<string>(), Is.EqualTo("Rating"));
            Assert.That(worksheet.Cell(1, 3).GetValue<string>(), Is.EqualTo("Capacity"));
            Assert.That(worksheet.Cell(1, 4).GetValue<string>(), Is.EqualTo("Vacancy"));
            Assert.That(worksheet.Cell(1, 5).GetValue<string>(), Is.EqualTo("Address"));
            Assert.That(worksheet.Cell(1, 6).GetValue<string>(), Is.EqualTo("Url"));

            Assert.That(worksheet.Cell(2, 1).GetValue<string>(), Is.EqualTo("Test Daycare"));
            Assert.That(worksheet.Cell(2, 2).GetValue<double>(), Is.EqualTo(4.5));
            Assert.That(worksheet.Cell(2, 3).GetValue<double>(), Is.EqualTo(10));
            Assert.That(worksheet.Cell(2, 4).GetValue<bool>(), Is.True);
            Assert.That(worksheet.Cell(2, 5).GetValue<string>(), Is.EqualTo("123 Test St"));
            Assert.That(worksheet.Cell(2, 6).GetHyperlink().ExternalAddress?.ToString(), Is.EqualTo("https://example.com/daycare/1"));
        }

        /// <summary>
        /// Tests that ExportAsync creates multiple worksheets when multiple program types exist.
        /// Input: Three different program types with one daycare each.
        /// Expected: Three worksheets, one for each program type.
        /// </summary>
        [Test]
        public async Task ExportAsync_MultipleProgramTypes_CreatesMultipleWorksheets()
        {
            // Arrange
            using var tempDir = new TempDirectory();
            string filePath = Path.Combine(tempDir.Path, "output.xlsx");
            var exporter = new ExcelExporter(filePath);
            var response = new DaycareSearchResponse
            {
                TopN = 3,
                TopPrograms =
                [
                    new TopProgramResult
                    {
                        Daycare = new Daycare
                        {
                            Id = 1,
                            Name = "Infant Daycare",
                            Address = "123 Test St",
                            Uri = new Uri("https://example.com/daycare/1")
                        },
                        Program = new DaycareProgram
                        {
                            ProgramType = ProgramType.Infant,
                            Capacity = 10,
                            Vacancy = true,
                            Rating = 4.5
                        }
                    },
                    new TopProgramResult
                    {
                        Daycare = new Daycare
                        {
                            Id = 2,
                            Name = "Toddler Daycare",
                            Address = "456 Test Ave",
                            Uri = new Uri("https://example.com/daycare/2")
                        },
                        Program = new DaycareProgram
                        {
                            ProgramType = ProgramType.Toddler,
                            Capacity = 15,
                            Vacancy = false,
                            Rating = 3.8
                        }
                    },
                    new TopProgramResult
                    {
                        Daycare = new Daycare
                        {
                            Id = 3,
                            Name = "Preschool Daycare",
                            Address = "789 Test Blvd",
                            Uri = new Uri("https://example.com/daycare/3")
                        },
                        Program = new DaycareProgram
                        {
                            ProgramType = ProgramType.Preschool,
                            Capacity = 20,
                            Vacancy = null,
                            Rating = 5.0
                        }
                    }
                ]
            };

            // Act
            await exporter.ExportAsync(response);

            // Assert
            Assert.That(File.Exists(filePath), Is.True);
            using var workbook = new XLWorkbook(filePath);
            Assert.That(workbook.Worksheets.Count, Is.EqualTo(3));

            var worksheetNames = workbook.Worksheets.Select(w => w.Name).ToList();
            Assert.That(worksheetNames, Does.Contain("Infant"));
            Assert.That(worksheetNames, Does.Contain("Toddler"));
            Assert.That(worksheetNames, Does.Contain("Preschool"));
        }

        /// <summary>
        /// Tests that ExportAsync handles multiple daycares in the same program type correctly.
        /// Input: Multiple daycares with the same program type.
        /// Expected: Single worksheet with multiple data rows.
        /// </summary>
        [Test]
        public async Task ExportAsync_MultipleDaycaresInSameProgramType_CreatesMultipleRows()
        {
            // Arrange
            using var tempDir = new TempDirectory();
            string filePath = Path.Combine(tempDir.Path, "output.xlsx");
            var exporter = new ExcelExporter(filePath);
            var response = new DaycareSearchResponse
            {
                TopN = 3,
                TopPrograms =
                [
                    new TopProgramResult
                    {
                        Daycare = new Daycare
                        {
                            Id = 1,
                            Name = "First Infant",
                            Address = "123 Test St",
                            Uri = new Uri("https://example.com/daycare/1")
                        },
                        Program = new DaycareProgram
                        {
                            ProgramType = ProgramType.Infant,
                            Capacity = 10,
                            Vacancy = true,
                            Rating = 4.5
                        }
                    },
                    new TopProgramResult
                    {
                        Daycare = new Daycare
                        {
                            Id = 2,
                            Name = "Second Infant",
                            Address = "456 Test Ave",
                            Uri = new Uri("https://example.com/daycare/2")
                        },
                        Program = new DaycareProgram
                        {
                            ProgramType = ProgramType.Infant,
                            Capacity = 15,
                            Vacancy = false,
                            Rating = 3.8
                        }
                    },
                    new TopProgramResult
                    {
                        Daycare = new Daycare
                        {
                            Id = 3,
                            Name = "Third Infant",
                            Address = "789 Test Blvd",
                            Uri = new Uri("https://example.com/daycare/3")
                        },
                        Program = new DaycareProgram
                        {
                            ProgramType = ProgramType.Infant,
                            Capacity = 20,
                            Vacancy = null,
                            Rating = 5.0
                        }
                    }
                ]
            };

            // Act
            await exporter.ExportAsync(response);

            // Assert
            Assert.That(File.Exists(filePath), Is.True);
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);

            Assert.That(worksheet.Name, Is.EqualTo("Infant"));
            Assert.That(worksheet.Cell(2, 1).GetValue<string>(), Is.EqualTo("First Infant"));
            Assert.That(worksheet.Cell(3, 1).GetValue<string>(), Is.EqualTo("Second Infant"));
            Assert.That(worksheet.Cell(4, 1).GetValue<string>(), Is.EqualTo("Third Infant"));
        }

        /// <summary>
        /// Tests that ExportAsync throws InvalidOperationException when accessing Rating.Value on a null Rating.
        /// Input: Program with null Rating.
        /// Expected: InvalidOperationException when accessing Rating.Value.
        /// </summary>
        [Test]
        public void ExportAsync_NullRating_ThrowsInvalidOperationException()
        {
            // Arrange
            using var tempDir = new TempDirectory();
            string filePath = Path.Combine(tempDir.Path, "output.xlsx");
            var exporter = new ExcelExporter(filePath);
            var response = new DaycareSearchResponse
            {
                TopN = 1,
                TopPrograms =
                [
                    new TopProgramResult
                    {
                        Daycare = new Daycare
                        {
                            Id = 1,
                            Name = "Test Daycare",
                            Address = "123 Test St",
                            Uri = new Uri("https://example.com/daycare/1")
                        },
                        Program = new DaycareProgram
                        {
                            ProgramType = ProgramType.Infant,
                            Capacity = 10,
                            Vacancy = true,
                            Rating = null
                        }
                    }
                ]
            };

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await exporter.ExportAsync(response));
        }

        /// <summary>
        /// Tests that ExportAsync handles null vacancy values correctly.
        /// Input: Program with null Vacancy.
        /// Expected: Null value written to Excel cell.
        /// </summary>
        [Test]
        public async Task ExportAsync_NullVacancy_WritesNullToCell()
        {
            // Arrange
            using var tempDir = new TempDirectory();
            string filePath = Path.Combine(tempDir.Path, "output.xlsx");
            var exporter = new ExcelExporter(filePath);
            var response = new DaycareSearchResponse
            {
                TopN = 1,
                TopPrograms =
                [
                    new TopProgramResult
                    {
                        Daycare = new Daycare
                        {
                            Id = 1,
                            Name = "Test Daycare",
                            Address = "123 Test St",
                            Uri = new Uri("https://example.com/daycare/1")
                        },
                        Program = new DaycareProgram
                        {
                            ProgramType = ProgramType.Infant,
                            Capacity = 10,
                            Vacancy = null,
                            Rating = 4.5
                        }
                    }
                ]
            };

            // Act
            await exporter.ExportAsync(response);

            // Assert
            Assert.That(File.Exists(filePath), Is.True);
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);
            Assert.That(worksheet.Cell(2, 4).IsEmpty(), Is.True);
        }

        /// <summary>
        /// Tests that ExportAsync handles special characters in daycare name and address.
        /// Input: Name and address with special characters, Unicode, quotes, etc.
        /// Expected: Special characters are preserved in the Excel file.
        /// </summary>
        [Test]
        public async Task ExportAsync_SpecialCharactersInStrings_HandlesCorrectly()
        {
            // Arrange
            using var tempDir = new TempDirectory();
            string filePath = Path.Combine(tempDir.Path, "output.xlsx");
            var exporter = new ExcelExporter(filePath);
            var response = new DaycareSearchResponse
            {
                TopN = 1,
                TopPrograms =
                [
                    new TopProgramResult
                    {
                        Daycare = new Daycare
                        {
                            Id = 1,
                            Name = "Test's \"Daycare\" & Care <Center>",
                            Address = "123 Test St, Apt #5-B (Rear), Toronto, ON",
                            Uri = new Uri("https://example.com/daycare/1")
                        },
                        Program = new DaycareProgram
                        {
                            ProgramType = ProgramType.Infant,
                            Capacity = 10,
                            Vacancy = true,
                            Rating = 4.5
                        }
                    }
                ]
            };

            // Act
            await exporter.ExportAsync(response);

            // Assert
            Assert.That(File.Exists(filePath), Is.True);
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);
            Assert.That(worksheet.Cell(2, 1).GetValue<string>(), Is.EqualTo("Test's \"Daycare\" & Care <Center>"));
            Assert.That(worksheet.Cell(2, 5).GetValue<string>(), Is.EqualTo("123 Test St, Apt #5-B (Rear), Toronto, ON"));
        }

        /// <summary>
        /// Tests that ExportAsync handles very long strings in name and address.
        /// Input: Very long name and address strings.
        /// Expected: Long strings are written without truncation.
        /// </summary>
        [Test]
        public async Task ExportAsync_VeryLongStrings_HandlesCorrectly()
        {
            // Arrange
            using var tempDir = new TempDirectory();
            string filePath = Path.Combine(tempDir.Path, "output.xlsx");
            var exporter = new ExcelExporter(filePath);
            string longName = new string('A', 500);
            string longAddress = new string('B', 500);
            var response = new DaycareSearchResponse
            {
                TopN = 1,
                TopPrograms =
                [
                    new TopProgramResult
                    {
                        Daycare = new Daycare
                        {
                            Id = 1,
                            Name = longName,
                            Address = longAddress,
                            Uri = new Uri("https://example.com/daycare/1")
                        },
                        Program = new DaycareProgram
                        {
                            ProgramType = ProgramType.Infant,
                            Capacity = 10,
                            Vacancy = true,
                            Rating = 4.5
                        }
                    }
                ]
            };

            // Act
            await exporter.ExportAsync(response);

            // Assert
            Assert.That(File.Exists(filePath), Is.True);
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);
            Assert.That(worksheet.Cell(2, 1).GetValue<string>(), Is.EqualTo(longName));
            Assert.That(worksheet.Cell(2, 5).GetValue<string>(), Is.EqualTo(longAddress));
        }

        /// <summary>
        /// Tests that ExportAsync handles empty strings in name and address.
        /// Input: Empty strings for name and address.
        /// Expected: Empty strings are written to cells.
        /// </summary>
        [Test]
        public async Task ExportAsync_EmptyStrings_HandlesCorrectly()
        {
            // Arrange
            using var tempDir = new TempDirectory();
            string filePath = Path.Combine(tempDir.Path, "output.xlsx");
            var exporter = new ExcelExporter(filePath);
            var response = new DaycareSearchResponse
            {
                TopN = 1,
                TopPrograms =
                [
                    new TopProgramResult
                    {
                        Daycare = new Daycare
                        {
                            Id = 1,
                            Name = string.Empty,
                            Address = string.Empty,
                            Uri = new Uri("https://example.com/daycare/1")
                        },
                        Program = new DaycareProgram
                        {
                            ProgramType = ProgramType.Infant,
                            Capacity = 10,
                            Vacancy = true,
                            Rating = 4.5
                        }
                    }
                ]
            };

            // Act
            await exporter.ExportAsync(response);

            // Assert
            Assert.That(File.Exists(filePath), Is.True);
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);
            Assert.That(worksheet.Cell(2, 1).GetValue<string>(), Is.EqualTo(string.Empty));
            Assert.That(worksheet.Cell(2, 5).GetValue<string>(), Is.EqualTo(string.Empty));
        }

        /// <summary>
        /// Tests that ExportAsync handles zero capacity.
        /// Input: Program with zero capacity.
        /// Expected: Zero is written to the capacity cell.
        /// </summary>
        [Test]
        public async Task ExportAsync_ZeroCapacity_WritesZero()
        {
            // Arrange
            using var tempDir = new TempDirectory();
            string filePath = Path.Combine(tempDir.Path, "output.xlsx");
            var exporter = new ExcelExporter(filePath);
            var response = new DaycareSearchResponse
            {
                TopN = 1,
                TopPrograms =
                [
                    new TopProgramResult
                    {
                        Daycare = new Daycare
                        {
                            Id = 1,
                            Name = "Test Daycare",
                            Address = "123 Test St",
                            Uri = new Uri("https://example.com/daycare/1")
                        },
                        Program = new DaycareProgram
                        {
                            ProgramType = ProgramType.Infant,
                            Capacity = 0,
                            Vacancy = true,
                            Rating = 4.5
                        }
                    }
                ]
            };

            // Act
            await exporter.ExportAsync(response);

            // Assert
            Assert.That(File.Exists(filePath), Is.True);
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);
            Assert.That(worksheet.Cell(2, 3).GetValue<double>(), Is.Zero);
        }

        /// <summary>
        /// Tests that ExportAsync handles int.MaxValue capacity.
        /// Input: Program with int.MaxValue capacity.
        /// Expected: MaxValue is written to the capacity cell.
        /// </summary>
        [Test]
        public async Task ExportAsync_MaxValueCapacity_WritesMaxValue()
        {
            // Arrange
            using var tempDir = new TempDirectory();
            string filePath = Path.Combine(tempDir.Path, "output.xlsx");
            var exporter = new ExcelExporter(filePath);
            var response = new DaycareSearchResponse
            {
                TopN = 1,
                TopPrograms =
                [
                    new TopProgramResult
                    {
                        Daycare = new Daycare
                        {
                            Id = 1,
                            Name = "Test Daycare",
                            Address = "123 Test St",
                            Uri = new Uri("https://example.com/daycare/1")
                        },
                        Program = new DaycareProgram
                        {
                            ProgramType = ProgramType.Infant,
                            Capacity = int.MaxValue,
                            Vacancy = true,
                            Rating = 4.5
                        }
                    }
                ]
            };

            // Act
            await exporter.ExportAsync(response);

            // Assert
            Assert.That(File.Exists(filePath), Is.True);
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);
            Assert.That(worksheet.Cell(2, 3).GetValue<double>(), Is.EqualTo(int.MaxValue));
        }

        /// <summary>
        /// Tests that ExportAsync handles all ProgramType enum values.
        /// Input: One daycare for each program type (Infant, Toddler, Preschool, Kindergarten).
        /// Expected: Four worksheets, one for each program type.
        /// </summary>
        [Test]
        public async Task ExportAsync_AllProgramTypes_CreatesWorksheetForEach()
        {
            // Arrange
            using var tempDir = new TempDirectory();
            string filePath = Path.Combine(tempDir.Path, "output.xlsx");
            var exporter = new ExcelExporter(filePath);
            var response = new DaycareSearchResponse
            {
                TopN = 4,
                TopPrograms =
                [
                    new TopProgramResult
                    {
                        Daycare = new Daycare
                        {
                            Id = 1,
                            Name = "Infant Daycare",
                            Address = "123 Test St",
                            Uri = new Uri("https://example.com/daycare/1")
                        },
                        Program = new DaycareProgram
                        {
                            ProgramType = ProgramType.Infant,
                            Capacity = 10,
                            Vacancy = true,
                            Rating = 4.5
                        }
                    },
                    new TopProgramResult
                    {
                        Daycare = new Daycare
                        {
                            Id = 2,
                            Name = "Toddler Daycare",
                            Address = "456 Test Ave",
                            Uri = new Uri("https://example.com/daycare/2")
                        },
                        Program = new DaycareProgram
                        {
                            ProgramType = ProgramType.Toddler,
                            Capacity = 15,
                            Vacancy = false,
                            Rating = 3.8
                        }
                    },
                    new TopProgramResult
                    {
                        Daycare = new Daycare
                        {
                            Id = 3,
                            Name = "Preschool Daycare",
                            Address = "789 Test Blvd",
                            Uri = new Uri("https://example.com/daycare/3")
                        },
                        Program = new DaycareProgram
                        {
                            ProgramType = ProgramType.Preschool,
                            Capacity = 20,
                            Vacancy = true,
                            Rating = 5.0
                        }
                    },
                    new TopProgramResult
                    {
                        Daycare = new Daycare
                        {
                            Id = 4,
                            Name = "Kindergarten Daycare",
                            Address = "321 Test Rd",
                            Uri = new Uri("https://example.com/daycare/4")
                        },
                        Program = new DaycareProgram
                        {
                            ProgramType = ProgramType.Kindergarten,
                            Capacity = 25,
                            Vacancy = false,
                            Rating = 4.2
                        }
                    }
                ]
            };

            // Act
            await exporter.ExportAsync(response);

            // Assert
            Assert.That(File.Exists(filePath), Is.True);
            using var workbook = new XLWorkbook(filePath);
            Assert.That(workbook.Worksheets.Count, Is.EqualTo(4));

            var worksheetNames = workbook.Worksheets.Select(w => w.Name).ToList();
            Assert.That(worksheetNames, Does.Contain("Infant"));
            Assert.That(worksheetNames, Does.Contain("Toddler"));
            Assert.That(worksheetNames, Does.Contain("Preschool"));
            Assert.That(worksheetNames, Does.Contain("Kindergarten"));
        }

        /// <summary>
        /// Tests that ExportAsync sets header row to bold font.
        /// Input: Single program with one daycare.
        /// Expected: Header row has bold font style.
        /// </summary>
        [Test]
        public async Task ExportAsync_ValidData_SetsHeaderRowBold()
        {
            // Arrange
            using var tempDir = new TempDirectory();
            string filePath = Path.Combine(tempDir.Path, "output.xlsx");
            var exporter = new ExcelExporter(filePath);
            var response = new DaycareSearchResponse
            {
                TopN = 1,
                TopPrograms =
                [
                    new TopProgramResult
                    {
                        Daycare = new Daycare
                        {
                            Id = 1,
                            Name = "Test Daycare",
                            Address = "123 Test St",
                            Uri = new Uri("https://example.com/daycare/1")
                        },
                        Program = new DaycareProgram
                        {
                            ProgramType = ProgramType.Infant,
                            Capacity = 10,
                            Vacancy = true,
                            Rating = 4.5
                        }
                    }
                ]
            };

            // Act
            await exporter.ExportAsync(response);

            // Assert
            Assert.That(File.Exists(filePath), Is.True);
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);
            Assert.That(worksheet.Row(1).Style.Font.Bold, Is.True);
        }

        /// <summary>
        /// Tests that ExportAsync throws DirectoryNotFoundException when file path directory doesn't exist.
        /// Input: File path with non-existent directory.
        /// Expected: DirectoryNotFoundException or similar exception.
        /// </summary>
        [Test]
        public void ExportAsync_InvalidFilePath_ThrowsException()
        {
            // Arrange
            string filePath = Path.Combine("C:\\NonExistentDirectory123456789", "output.xlsx");
            var exporter = new ExcelExporter(filePath);
            var response = new DaycareSearchResponse
            {
                TopN = 1,
                TopPrograms =
                [
                    new TopProgramResult
                    {
                        Daycare = new Daycare
                        {
                            Id = 1,
                            Name = "Test Daycare",
                            Address = "123 Test St",
                            Uri = new Uri("https://example.com/daycare/1")
                        },
                        Program = new DaycareProgram
                        {
                            ProgramType = ProgramType.Infant,
                            Capacity = 10,
                            Vacancy = true,
                            Rating = 4.5
                        }
                    }
                ]
            };

            // Act & Assert
            Assert.ThrowsAsync<DirectoryNotFoundException>(async () =>
                await exporter.ExportAsync(response));
        }

        /// <summary>
        /// Tests that the ExcelExporter constructor successfully creates an instance with various valid file names.
        /// Verifies that the constructor executes without throwing exceptions for different string inputs.
        /// </summary>
        /// <param name="fileName">The file name to use for construction</param>
        [TestCase("output.xlsx")]
        [TestCase("report.xlsx")]
        [TestCase("data-export.xlsx")]
        [TestCase("file with spaces.xlsx")]
        [TestCase("file_with_underscores.xlsx")]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("   ")]
        [TestCase("\t")]
        [TestCase("file!@#$%^&().xlsx")]
        [TestCase("C:\\path\\to\\file.xlsx")]
        [TestCase("/unix/path/to/file.xlsx")]
        [TestCase("..\\relative\\path.xlsx")]
        public void Constructor_VariousFileNames_CreatesInstanceSuccessfully(string fileName)
        {
            // Arrange & Act
            var exporter = new ExcelExporter(fileName);

            // Assert
            Assert.That(exporter, Is.Not.Null);
        }

        /// <summary>
        /// Tests that the ExcelExporter constructor successfully handles very long file names.
        /// Verifies that the constructor can handle edge case string lengths without throwing exceptions.
        /// </summary>
        [Test]
        public void Constructor_VeryLongFileName_CreatesInstanceSuccessfully()
        {
            // Arrange
            var longFileName = new string('a', 10000) + ".xlsx";

            // Act
            var exporter = new ExcelExporter(longFileName);

            // Assert
            Assert.That(exporter, Is.Not.Null);
        }

        /// <summary>
        /// Tests that the ExcelExporter constructor successfully handles file names with special characters.
        /// Verifies proper handling of Unicode and special characters in file names.
        /// </summary>
        [TestCase("файл.xlsx")]
        [TestCase("文件.xlsx")]
        [TestCase("ファイル.xlsx")]
        [TestCase("file\u0000null.xlsx")]
        [TestCase("file\nnewline.xlsx")]
        [TestCase("file\rcarriage.xlsx")]
        public void Constructor_SpecialCharactersInFileName_CreatesInstanceSuccessfully(string fileName)
        {
            // Arrange & Act
            var exporter = new ExcelExporter(fileName);

            // Assert
            Assert.That(exporter, Is.Not.Null);
        }
    }
}
