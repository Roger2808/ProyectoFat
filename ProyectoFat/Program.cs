using System;
using System.IO;
using System.Text;
using System.Text.Json;

class FATFile
{
    public string FileName { get; set; }
    public string DataFilePath { get; set; }
    public bool InRecycleBin { get; set; }
    public int TotalCharacters { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime? ModificationDate { get; set; }
    public DateTime? DeletionDate { get; set; }

    public FATFile(string fileName, string dataFilePath)
    {
        FileName = fileName;
        DataFilePath = dataFilePath;
        InRecycleBin = false;
        CreationDate = DateTime.Now;
        TotalCharacters = 0;
    }
}

class DataChunk
{
    public string Data { get; set; }
    public string? NextFile { get; set; }
    public bool EOF { get; set; }

    public DataChunk(string data, string? nextFile, bool eof)
    {
        Data = data;
        NextFile = nextFile;
        EOF = eof;
    }
}

class Program
{
    static string fatDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "/FATFiles";
    static string dataDirectory = fatDirectory + "/DataChunks";
    static string fatTableFilePath = fatDirectory + "/FATTable.txt";

    static void Main()
    {
        Directory.CreateDirectory(fatDirectory);
        Directory.CreateDirectory(dataDirectory);

        while (true)
        {
            Console.Clear();
            Console.WriteLine("MENU\n1. Crear Archivo\n2. Listar Archivos\n3. Abrir Archivo\n4. Modificar Archivo\n5. Eliminar Archivo\n6. Recuperar Archivo\n7. Salir");
            string option = Console.ReadLine()!;

            switch (option)
            {
                case "1":
                    CrearArchivo();
                    break;
                case "2":
                    ListarArchivos();
                    break;
                case "3":
                    AbrirArchivo();
                    break;
                case "4":
                    ModificarArchivo();
                    break;
                case "5":
                    EliminarArchivo();
                    break;
                case "6":
                    RecuperarArchivo();
                    break;
                case "7":
                    return;
                default:
                    Console.WriteLine("Opción inválida.");
                    break;
            }
        }
    }

    static void CrearArchivo()
    {
        Console.Clear();
        Console.WriteLine("Ingrese el nombre del archivo:");
        string fileName = Console.ReadLine()!;

        Console.WriteLine("Ingrese los datos del archivo (máximo 20 caracteres por fragmento):");
        string data = Console.ReadLine()!;

        string dataFilePath = GuardarDatos(data);

        // Crear el archivo FAT
        FATFile fatFile = new FATFile(fileName, dataFilePath)
        {
            TotalCharacters = data.Length
        };

        // Serializar la tabla FAT y guardarla en un archivo de texto
        string fatJson = JsonSerializer.Serialize(fatFile);
        File.AppendAllText(fatTableFilePath, fatJson + Environment.NewLine);
        Console.WriteLine("Archivo creado y guardado con éxito.");
        Console.ReadKey();
    }

    static void ListarArchivos()
    {
        Console.Clear();
        if (!File.Exists(fatTableFilePath))
        {
            Console.WriteLine("No hay archivos disponibles.");
            Console.ReadKey();
            return;
        }

        string[] fatEntries = File.ReadAllLines(fatTableFilePath);
        int index = 1;
        foreach (string entry in fatEntries)
        {
            FATFile fatFile = JsonSerializer.Deserialize<FATFile>(entry)!;

            if (!fatFile.InRecycleBin)
            {
                Console.WriteLine($"{index}. Nombre: {fatFile.FileName} | Tamaño: {fatFile.TotalCharacters} caracteres | Creado: {fatFile.CreationDate} | Modificado: {fatFile.ModificationDate}");
                index++;
            }
        }
        Console.ReadKey();
    }

    static void AbrirArchivo()
    {
        Console.Clear();
        FATFile? fatFile = SeleccionarArchivo();
        if (fatFile == null) return;

        Console.WriteLine($"Archivo: {fatFile.FileName} | Tamaño: {fatFile.TotalCharacters} caracteres");
        Console.WriteLine("Contenido:");
        string contenido = LeerDatos(fatFile.DataFilePath);
        Console.WriteLine(contenido);
        Console.ReadKey();
    }

    static void ModificarArchivo()
    {
        ListarArchivos();
        Console.WriteLine("Seleccione el número del archivo que desea modificar:");
        string selectedFile = Console.ReadLine()!;

        // Buscar archivo en FAT
        string[] fatEntries = File.ReadAllLines(fatTableFilePath);
        FATFile? archivoFAT = null;
        foreach (string entry in fatEntries)
        {
            FATFile file = JsonSerializer.Deserialize<FATFile>(entry)!;
            if (file.FileName == selectedFile && !file.InRecycleBin)
            {
                archivoFAT = file;
                break;
            }
        }

        if (archivoFAT != null)
        {
            // Leer y mostrar el contenido actual
            Console.Clear();
            Console.WriteLine($"Archivo: {archivoFAT.FileName}");
            Console.WriteLine($"Tamaño: {archivoFAT.TotalCharacters} caracteres");
            Console.WriteLine($"Contenido actual:");
            string currentData = LeerDatos(archivoFAT.DataFilePath);
            Console.WriteLine(currentData);

            // Solicitar nuevo contenido
            Console.WriteLine("\nIngrese el nuevo contenido (presione ESC para terminar):");
            StringBuilder nuevoContenido = new StringBuilder();
            ConsoleKeyInfo key;

            // Capturar entrada de texto hasta que el usuario presione ESC
            while ((key = Console.ReadKey(true)).Key != ConsoleKey.Escape)
            {
                // Agregar caracteres al nuevo contenido
                nuevoContenido.Append(key.KeyChar);
                Console.Write(key.KeyChar); // Muestra el carácter en pantalla
            }

            // Solicitar confirmación para guardar cambios
            Console.WriteLine("\n\n¿Desea guardar los cambios? (s/n):");
            string confirmar = Console.ReadLine()!.ToLower();

            if (confirmar == "s")
            {
                // Eliminar archivos antiguos
                BorrarDatos(archivoFAT.DataFilePath);

                // Guardar nuevos datos
                archivoFAT.DataFilePath = GuardarDatos(nuevoContenido.ToString());
                archivoFAT.TotalCharacters = nuevoContenido.Length;
                archivoFAT.ModificationDate = DateTime.Now;

                // Actualizar FAT
                ActualizarFAT(archivoFAT);

                Console.WriteLine("Los cambios han sido guardados.");
            }
            else
            {
                Console.WriteLine("Los cambios no fueron guardados.");
            }
        }
        else
        {
            Console.WriteLine("No se encontró el archivo.");
        }
        Console.ReadKey();
    }


    static void EliminarArchivo()
    {
        Console.Clear();
        FATFile? fatFile = SeleccionarArchivo();
        if (fatFile == null) return;

        fatFile.InRecycleBin = true;
        fatFile.DeletionDate = DateTime.Now;

        ActualizarFAT(fatFile);
        Console.WriteLine($"Archivo {fatFile.FileName} enviado a la Papelera.");
        Console.ReadKey();
    }

    static void RecuperarArchivo()
    {
        Console.Clear();
        if (!File.Exists(fatTableFilePath))
        {
            Console.WriteLine("No hay archivos en la Papelera.");
            Console.ReadKey();
            return;
        }

        string[] fatEntries = File.ReadAllLines(fatTableFilePath);
        int index = 1;
        foreach (string entry in fatEntries)
        {
            FATFile fatFile = JsonSerializer.Deserialize<FATFile>(entry)!;

            if (fatFile.InRecycleBin)
            {
                Console.WriteLine($"{index}. Nombre: {fatFile.FileName} | Tamaño: {fatFile.TotalCharacters} | Eliminado: {fatFile.DeletionDate}");
                index++;
            }
        }

        Console.WriteLine("Ingrese el número del archivo a recuperar:");
        int seleccionado = Convert.ToInt32(Console.ReadLine()) - 1;

        if (seleccionado >= 0 && seleccionado < fatEntries.Length)
        {
            FATFile fatFile = JsonSerializer.Deserialize<FATFile>(fatEntries[seleccionado])!;
            fatFile.InRecycleBin = false;
            fatFile.ModificationDate = DateTime.Now;

            ActualizarFAT(fatFile);
            Console.WriteLine($"Archivo {fatFile.FileName} recuperado.");
        }
        else
        {
            Console.WriteLine("Selección inválida.");
        }
        Console.ReadKey();
    }

    static FATFile? SeleccionarArchivo()
    {
        if (!File.Exists(fatTableFilePath))
        {
            Console.WriteLine("No hay archivos disponibles.");
            Console.ReadKey();
            return null;
        }

        string[] fatEntries = File.ReadAllLines(fatTableFilePath);
        int index = 1;
        foreach (string entry in fatEntries)
        {
            FATFile fatFile = JsonSerializer.Deserialize<FATFile>(entry)!;
            if (!fatFile.InRecycleBin)
            {
                Console.WriteLine($"{index}. Nombre: {fatFile.FileName} | Tamaño: {fatFile.TotalCharacters}");
                index++;
            }
        }

        Console.WriteLine("Ingrese el número del archivo que desea abrir:");
        int seleccionado = Convert.ToInt32(Console.ReadLine()) - 1;

        if (seleccionado >= 0 && seleccionado < fatEntries.Length)
        {
            return JsonSerializer.Deserialize<FATFile>(fatEntries[seleccionado])!;
        }
        else
        {
            Console.WriteLine("Selección inválida.");
            Console.ReadKey();
            return null;
        }
    }

    static string GuardarDatos(string data)
    {
        string firstFilePath = "";
        string? previousFilePath = null;

        for (int i = 0; i < data.Length; i += 20)
        {
            string chunkData = data.Substring(i, Math.Min(20, data.Length - i));
            bool isEOF = (i + 20) >= data.Length;
            string? nextFilePath = isEOF ? null : dataDirectory + "/chunk_" + Guid.NewGuid().ToString() + ".txt";
            DataChunk chunk = new DataChunk(chunkData, nextFilePath, isEOF);
            string chunkJson = JsonSerializer.Serialize(chunk);

            string chunkFilePath = dataDirectory + "/chunk_" + Guid.NewGuid().ToString() + ".txt";
            File.WriteAllText(chunkFilePath, chunkJson);

            if (i == 0)
                firstFilePath = chunkFilePath;
            if (previousFilePath != null)
            {
                string previousJson = File.ReadAllText(previousFilePath);
                DataChunk previousChunk = JsonSerializer.Deserialize<DataChunk>(previousJson)!;
                previousChunk.NextFile = chunk.ToString();
                previousChunk.NextFile = chunkFilePath;
                previousJson = JsonSerializer.Serialize(previousChunk);
                File.WriteAllText(previousFilePath, previousJson);
            }

            previousFilePath = chunkFilePath;
        }

        return firstFilePath;
    }

    static string LeerDatos(string filePath)
    {
        string data = "";
        string? currentFilePath = filePath;

        while (currentFilePath != null)
        {
            string chunkJson = File.ReadAllText(currentFilePath);
            DataChunk chunk = JsonSerializer.Deserialize<DataChunk>(chunkJson)!;

            data += chunk.Data;
            currentFilePath = chunk.NextFile;
        }

        return data;
    }

    static void BorrarDatos(string filePath)
    {
        string? currentFilePath = filePath;

        while (currentFilePath != null)
        {
            string chunkJson = File.ReadAllText(currentFilePath);
            DataChunk chunk = JsonSerializer.Deserialize<DataChunk>(chunkJson)!;

            File.Delete(currentFilePath);
            currentFilePath = chunk.NextFile;
        }
    }

    static void ActualizarFAT(FATFile updatedFile)
    {
        string[] fatEntries = File.ReadAllLines(fatTableFilePath);
        for (int i = 0; i < fatEntries.Length; i++)
        {
            FATFile fatFile = JsonSerializer.Deserialize<FATFile>(fatEntries[i])!;
            if (fatFile.FileName == updatedFile.FileName)
            {
                fatEntries[i] = JsonSerializer.Serialize(updatedFile);
                break;
            }
        }
        File.WriteAllLines(fatTableFilePath, fatEntries);
    }
}
