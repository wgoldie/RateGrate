# RateGrate
A library for intelligent rate limit handling, based on a new design of how clients should interpret and handle API rate limits.

Goals include: methods/tasks/attributes/middleware/etc for limiting api queries (including LINQ support), tools like token pooling for using/abusing rate limited apis to their fullest potential, reporting information like rate limit delays and expected execution time in-progress tasks.

Currently in development.

The VS solution consists of:
- The RateGrate library itself. 
  This will probably target a .NET dll, and will more broadly serve as a implementation example for the RateGrate "design pattern".
- The RateGrateTests project, which includes internal unit tests for the RateGrate Library
- A TestApiServer project, which will be used to simulate a generic or potentially emulate real apis (twitter, facebook, etc)
  for external testing purposes.
- A TestClient project, which will serve as an example project integrating the RateGrate library,
  and will include and/or consist of external unit tests for the library's functions, 
  with the goal of 100% test and example coverage (hopefully these will be the same thing).
  

please contrib
