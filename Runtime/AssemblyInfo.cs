using System.Runtime.CompilerServices;

// Lets the development project's play-mode tests check internal logic (not shipped with the package).
[assembly: InternalsVisibleTo("ClassroomClient.Harness.Tests")]
